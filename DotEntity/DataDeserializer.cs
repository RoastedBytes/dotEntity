﻿// #region Author Information
// // DataDeserializer.cs
// // 
// // (c) Apexol Technologies. All Rights Reserved.
// // 
// #endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DotEntity.Extensions;
using DotEntity.Reflection;

namespace DotEntity
{
    internal class DataDeserializer<T> : IDataDeserializer<T> where T : class
    {
        private ConcurrentDictionary<string, object> _typeMap = null;
        private ConcurrentDictionary<string, object> _getterMap = null;
        private readonly Type _typeofT;

        public DataDeserializer()
        {
            _typeofT = typeof(T);
            CreateMapIfNotDone();
        }

        private void CreateMapIfNotDone()
        {
            if (_typeMap != null && _typeMap.Count > 0)
                return;

            _typeMap = new ConcurrentDictionary<string, object>();
            //exclude virtual properties
            var typeProperties = _typeofT.GetDatabaseUsableProperties();
            foreach (var property in typeProperties)
            {

                var propertyType = property.PropertyType;
                object setter = null;
                if (propertyType == typeof(int))
                    setter = property.CreateSetter<T, int>();
                else if (propertyType == typeof(string))
                    setter = property.CreateSetter<T, string>();
                else if (propertyType == typeof(DateTime))
                    setter = property.CreateSetter<T, DateTime>();
                else if (propertyType == typeof(decimal))
                    setter = property.CreateSetter<T, decimal>();
                else if (propertyType == typeof(bool))
                    setter = property.CreateSetter<T, bool>();

                _typeMap.TryAdd(property.Name, setter);
            }
        }
      

        
        public T DeserializeSingle(IDataReader reader)
        {
            return DeserializeMany(reader).FirstOrDefault();
        }

        public T DeserializeSingle(List<DataReaderRow> rows)
        {
            return DeserializeMany(rows).FirstOrDefault();
        }

        public IEnumerable<T> DeserializeMany(IDataReader reader)
        {
            var columnNames = GetColumns();
            var rows = reader.GetDataReaderRows(columnNames, _typeofT.Name);
            return DeserializeMany(rows);
        }

        public IEnumerable<T> DeserializeMany(List<DataReaderRow> rows)
        {
            var tArray = FurnishInstances(rows);
            return tArray;
        }

        public void SetProperties(T instance, DataReaderRow row, string[] columnNames)
        {
            for(var i = 0; i < columnNames.Length; i++)
            {
                var fieldName = columnNames[i];
                if (!_typeMap.ContainsKey(fieldName)) continue;
                var fieldValue = row[_typeofT.Name + "." + fieldName];
                var fieldType = fieldValue.GetType();

                if (fieldType == typeof(int))
                    SetPropertyAs<int>(instance, fieldName, fieldValue);
                else if (fieldType == typeof(string))
                    SetPropertyAs<string>(instance, fieldName, fieldValue);
                else if (fieldType == typeof(DateTime))
                    SetPropertyAs<DateTime>(instance, fieldName, fieldValue);
                else if (fieldType == typeof(decimal))
                    SetPropertyAs<decimal>(instance, fieldName, fieldValue);
                else if (fieldType == typeof(bool))
                    SetPropertyAs<bool>(instance, fieldName, fieldValue);
            }
        }
       
        private IEnumerable<T> FurnishInstances(List<DataReaderRow> rows)
        {
            var columnNames = GetColumns();
            var tInstances = Instantiator<T>.Instances(rows.Count);
            var index = 0;

            foreach (var row in rows)
            {
                var instance = tInstances[index++];
                SetProperties(instance, row, columnNames);
                yield return instance;
            }
        }

        public IEnumerable<T> DeserializeManyNested(IDataReader reader, IList<IJoinMeta> joinMetas, Dictionary<Type, Delegate> relationActions)
        {
            
            var tInstances = new List<T>();
            var tColumns = GetColumns();
            //make deserializers for each of relation types
            var deserializers = new Dictionary<Type, IDataDeserializer>();
            var columnsToSkip = new Dictionary<Type, int> {{_typeofT, 0}};
            var localObjectCache = new Dictionary<string, object>();

            var toSkip = tColumns.Length;
            foreach (var jm in joinMetas)
            {
                var serializerObject = (IDataDeserializer) GenericInvoker.InvokeProperty(null, typeof(DataDeserializer<>), jm.OnType, "Instance");
                deserializers.Add(jm.OnType, serializerObject);
                columnsToSkip.Add(jm.OnType, toSkip);
                toSkip += serializerObject.GetColumns().Length;
            }

            var rowIndex = 0;
            DataReaderRow prevRow = null;
            var rows = reader.GetDataReaderRows(columnsToSkip); //all rows
            var lastProcesseedObjects = new Dictionary<Type, object>();

            void AddOrUpdateLastProcessObject(Type type, object obj)
            {
                if (lastProcesseedObjects.ContainsKey(type))
                    lastProcesseedObjects[type] = obj;
                else
                {
                    lastProcesseedObjects.Add(type, obj);
                }
            }

            while (rowIndex < rows.Count)
            {
                var row = rows[rowIndex];
                //first create the root instances
                if (CreateInstanceIfRequired(_typeofT, prevRow, row, this, 0, out object tInstance, ref localObjectCache))
                {
                    tInstances.Add((T) tInstance);
                    AddOrUpdateLastProcessObject(_typeofT, tInstance);
                }
                else
                {
                    tInstance = lastProcesseedObjects[_typeofT];
                }

                //then for all the child instances
                foreach (var ds in deserializers)
                {
                    if (!relationActions.ContainsKey(ds.Key))
                        continue;

                    if (CreateInstanceIfRequired(ds.Key, prevRow, row, ds.Value, columnsToSkip[ds.Key], out object childInstance, ref localObjectCache))
                    {
                        //invoke the relation to bind the instances if required
                        relationActions[ds.Key].DynamicInvoke(tInstance, childInstance);
                        AddOrUpdateLastProcessObject(ds.Key, childInstance);
                    }
                }
                prevRow = row;
                rowIndex++;
            }

            return tInstances.AsEnumerable();
        }

        private bool CreateInstanceIfRequired(Type instanceType, DataReaderRow prevDataRow, DataReaderRow currentDataRow, IDataDeserializer deserializer, int skipColumns, out object newInstance, ref Dictionary<string, object> localCache)
        {
            const string localObjectKey = "{0}.{1}.{2}"; //<Type>.<Key>.<NUM>

            newInstance = null;
            var columns = deserializer.GetColumns();
            var typedColumns = deserializer.GetTypedColumnNames(columns, instanceType);
            if (DataReaderRow.AreSameRowsForColumns(prevDataRow, currentDataRow, typedColumns, skipColumns))
                return false;

            //are all columns of current row null
            if (DataReaderRow.AreAllColumnsNull(currentDataRow, typedColumns, skipColumns))
                return false;

            //let's check if have this object in cache
            var keyColumn = deserializer.GetKeyColumn();
            var cacheKey = string.Format(localObjectKey, instanceType.Name, keyColumn,
                currentDataRow[_typeofT.Name + "." + keyColumn]);

            if (localCache.TryGetValue(cacheKey, out newInstance))
                return true;

            //we can create instance
            newInstance = Instantiator.GetInstance(instanceType);
            
            //assign properties
            GenericInvoker.Invoke(deserializer, "SetProperties", newInstance, currentDataRow, columns);
            localCache.Add(cacheKey, newInstance);
            return true;
        }

        internal void SetPropertyAs<TType>(T instance, string fieldName, object value)
        {
            ((Action<T, TType>)_typeMap[fieldName]).Invoke(instance, Parse<TType>(value));
        }

        internal TType GetPropertyAs<TType>(T instance, string fieldName)
        {
            if(_getterMap == null)
                _getterMap = new ConcurrentDictionary<string, object>();

            if(!_getterMap.TryGetValue(fieldName, out object getter))
            {

                var propertyInfo = _typeofT.GetProperty(fieldName);
                if(propertyInfo == null)
                    throw new Exception("Can't find property");

                getter = propertyInfo.CreateGetter<T, TType>();
            }
            return ((Func<T, TType>) getter).Invoke(instance);
        }

        private static TType Parse<TType>(object value)
        {
            if (value == null || value is DBNull) return default(TType);
            if (value is TType) return (TType)value;
            var type = typeof(TType);
            type = Nullable.GetUnderlyingType(type) ?? type;
#if NETSTANDARD15
            if (type.GetTypeInfo().IsEnum)
#else
            if (type.IsEnum)
#endif
            {
                if (value is float || value is double || value is decimal)
                {
                    value = Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);
                }
                return (TType)Enum.ToObject(type, value);
            }

            return (TType)Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }


        public static DataDeserializer<T> Instance => Singleton<DataDeserializer<T>>.Instance;

        private string[] _columnsAsArray;
        public string[] GetColumns()
        {
            return _columnsAsArray ?? (_columnsAsArray = _typeMap.Keys.ToArray());
        }

        private string _keyColumnName = null;
        public string GetKeyColumn()
        {
            if (_keyColumnName != null)
                return _keyColumnName;
            _keyColumnName = _typeofT.GetKeyColumnName();
            return _keyColumnName;
        }
    }
}
﻿/**
 * Copyright(C) 2017  Apexol Technologies
 * 
 * This file (DefaultDatabaseTableGenerator.cs) is part of dotEntity(https://github.com/RoastedBytes/dotentity).
 * 
 * dotEntity is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 
 * dotEntity is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * GNU Affero General Public License for more details.
 
 * You should have received a copy of the GNU Affero General Public License
 * along with dotEntity.If not, see<http://www.gnu.org/licenses/>.

 * You can release yourself from the requirements of the AGPL license by purchasing
 * a commercial license (dotEntity Pro). Buying such a license is mandatory as soon as you
 * develop commercial activities involving the dotEntity software without
 * disclosing the source code of your own applications. The activites include:
 * shipping dotEntity with a closed source product, offering paid services to customers
 * as an Application Service Provider.
 * To know more about our commercial license email us at support@roastedbytes.com or
 * visit http://dotentity.net/licensing
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DotEntity.Extensions;

namespace DotEntity
{
    public class DefaultDatabaseTableGenerator : IDatabaseTableGenerator
    {
        private readonly ITypeMapProvider _typeMapProvider;
        public DefaultDatabaseTableGenerator()
        {
            _typeMapProvider = DotEntityDb.Provider.TypeMapProvider;
        }
        #region typemap

        protected Dictionary<Type, string> TypeMap => _typeMapProvider.TypeMap;
        
        #endregion

        public virtual string GetFormattedDbTypeForType(Type type, int maxLength = 0)
        {
            Throw.IfInvalidDataTypeMapping(!TypeMap.TryGetValue(type, out string dbTypeString), type);
            var typeBuilder = new StringBuilder(dbTypeString);
            if (maxLength > 0)
                typeBuilder.Append($"({maxLength})");
            else if(type == typeof(string))
                typeBuilder.Append($"(MAX)");
            typeBuilder.Append(type.IsNullable() ? " NULL" : " NOT NULL");
            return typeBuilder.ToString();
        }

        public virtual string GetCreateTableScript<T>()
        {
            return GetCreateTableScript(typeof(T));
        }

        public virtual string GetCreateTableScript(Type type)
        {
            var tableName = DotEntityDb.GetTableNameForType(type);
            var properties = type.GetDatabaseUsableProperties();

            var builder = new StringBuilder($"CREATE TABLE {tableName.ToEnclosed()}{Environment.NewLine}(");
            var keyColumn = type.GetKeyColumnName();

            //is key column nullable
            var propertyInfos = properties as PropertyInfo[] ?? properties.ToArray();
            Throw.IfKeyTypeNullable(propertyInfos.First(x => x.Name == keyColumn).PropertyType, keyColumn);

            foreach (var property in propertyInfos)
            {
                var pType = property.PropertyType;
                var fieldName = property.Name;
                var dbFieldType = GetFormattedDbTypeForType(pType);
                var identityString = "";
                //do we have key attribute here?
                if (fieldName == keyColumn && pType == typeof(int))
                {
                    identityString = " IDENTITY(1,1)";
                }
                builder.Append($"\t {fieldName.ToEnclosed()} {dbFieldType}{identityString},");
                builder.Append(Environment.NewLine);
            }
            builder.Append($"PRIMARY KEY CLUSTERED ({keyColumn.ToEnclosed()} ASC));");
            return builder.ToString();
        }

        public virtual string GetDropTableScript<T>()
        {
            return GetDropTableScript(typeof(T));
        }

        public virtual string GetDropTableScript(Type type)
        {
            var tableName = DotEntityDb.GetTableNameForType(type);
            return $"DROP TABLE {tableName.ToEnclosed()};";
        }

        public virtual string GetCreateConstraintScript(Relation relation)
        {
            var fromTable = DotEntityDb.GetTableNameForType(relation.SourceType);
            var toTable = DotEntityDb.GetTableNameForType(relation.DestinationType);
            var constraintName = GetForeignKeyConstraintName(fromTable, toTable, relation.SourceColumnName,
                relation.DestinationColumnName);

            var builder = new StringBuilder($"ALTER TABLE {toTable.ToEnclosed()}{Environment.NewLine}");
            builder.Append($"ADD CONSTRAINT {constraintName.ToEnclosed()}{Environment.NewLine}");
            builder.Append($"FOREIGN KEY ({relation.DestinationColumnName.ToEnclosed()}) REFERENCES {fromTable.ToEnclosed()}({relation.SourceColumnName.ToEnclosed()});");
            return builder.ToString();
        }

        public virtual string GetDropConstraintScript(Relation relation)
        {
            var fromTable = DotEntityDb.GetTableNameForType(relation.SourceType);
            var toTable = DotEntityDb.GetTableNameForType(relation.DestinationType);
            var constraintName = GetForeignKeyConstraintName(fromTable, toTable, relation.SourceColumnName,
                relation.DestinationColumnName);
            var builder = new StringBuilder($"ALTER TABLE {toTable.ToEnclosed()}{Environment.NewLine}");
            builder.Append($"DROP CONSTRAINT {constraintName.ToEnclosed()};");
            return builder.ToString();
        }

        public virtual string GetAddColumnScript(Type type, string columnName, Type columnType, int maxLength = 0)
        {
            var tableName = DotEntityDb.GetTableNameForType(type);
            var dataTypeString = GetFormattedDbTypeForType(columnType, maxLength);
            var builder = new StringBuilder($"ALTER TABLE {tableName.ToEnclosed()}{Environment.NewLine}");
            builder.Append($"ADD COLUMN {columnName.ToEnclosed()} {dataTypeString}");
            return builder.ToString();
        }

        public virtual string GetDropColumnScript(Type type, string columnName)
        {
            var tableName = DotEntityDb.GetTableNameForType(type);
            var builder = new StringBuilder($"ALTER TABLE [{tableName.ToEnclosed()}]{Environment.NewLine}");
            builder.Append($"DROP COLUMN {columnName.ToEnclosed()};");
            return builder.ToString();
        }

        public virtual string GetAlterColumnScript(Type type, string columnName, Type columnType, int maxLength = 0)
        {
            var tableName = DotEntityDb.GetTableNameForType(type);
            var dataTypeString = GetFormattedDbTypeForType(columnType, maxLength);
            var builder = new StringBuilder($"ALTER TABLE [{tableName.ToEnclosed()}]{Environment.NewLine}");
            builder.Append($"ALTER COLUMN {columnName.ToEnclosed()} {dataTypeString}");
            return builder.ToString();
        }

        protected static string GetForeignKeyConstraintName(string fromTable, string toTable, string fromId, string toId)
        {
            const string constraintKey = "FK_{0}_{1}_{2}_{3}";
            return string.Format(constraintKey, fromTable, fromId, toTable, toId);
        }
    }
}
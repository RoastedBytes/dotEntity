﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using DotEntity.Enumerations;
using DotEntity.Extensions;

namespace DotEntity.MySql
{
    public class MySqlQueryGenerator : DefaultQueryGenerator
    {
        public override string GenerateInsert(string tableName, object entity, out IList<QueryInfo> parameters)
        {
            Dictionary<string, object> columnValueMap = QueryParserUtilities.ParseObjectKeyValues(entity, exclude: "Id");
            var insertColumns = columnValueMap.Keys.ToArray();
            var joinInsertString = string.Join(",", insertColumns.Select(x => x.ToEnclosed()));
            var joinValueString = "@" + string.Join(",@", insertColumns); ;
            parameters = ToQueryInfos(columnValueMap);

            return $"INSERT INTO {tableName.ToEnclosed()} ({joinInsertString}) VALUES ({joinValueString});SELECT last_insert_id() AS { "Id".ToEnclosed()};";
        }

        public override string GenerateSelect<T>(out IList<QueryInfo> parameters, List<Expression<Func<T, bool>>> where = null, Dictionary<Expression<Func<T, object>>, RowOrder> orderBy = null, int page = 1, int count = int.MaxValue)
        {
            parameters = new List<QueryInfo>();
            var builder = new StringBuilder();
            var tableName = DotEntityDb.GetTableNameForType<T>();

            var whereString = "";

            if (where != null)
            {
                var whereStringBuilder = new List<string>();
                foreach (var wh in where)
                {
                    var parser = new ExpressionTreeParser(wh);
                    whereStringBuilder.Add(parser.GetWhereString());
                    var queryParameters = parser.QueryInfoList;
                    parameters = parameters.Concat(queryParameters).ToList();
                }
                whereString = string.Join(" AND ", whereStringBuilder).Trim();
            }

            var orderByStringBuilder = new List<string>();
            var orderByString = "";
            if (orderBy != null)
            {
                foreach (var ob in orderBy)
                {
                    var parser = new ExpressionTreeParser(ob.Key);
                    orderByStringBuilder.Add(parser.GetOrderByString() + (ob.Value == RowOrder.Descending ? " DESC" : ""));
                }

                orderByString = string.Join(", ", orderByStringBuilder).Trim(',');
            }
         
            // make the query now
            builder.Append($"SELECT * FROM ");
            builder.Append(tableName.ToEnclosed());

            if (!string.IsNullOrEmpty(whereString))
            {
                builder.Append(" WHERE " + whereString);
            }

            if (!string.IsNullOrEmpty(orderByString))
            {
                builder.Append(" ORDER BY " + orderByString);
            }
            if (page > 1 || count != int.MaxValue)
            {
                var offset = (page - 1) * count;
                builder.Append($" LIMIT {offset},{count}");
            }
            var query = builder.ToString().Trim() + ";";
            return query;
        }

        public override string GenerateSelectWithTotalMatchingCount<T>(out IList<QueryInfo> parameters, List<Expression<Func<T, bool>>> @where = null, Dictionary<Expression<Func<T, object>>, RowOrder> orderBy = null,
            int page = 1, int count = Int32.MaxValue)
        {
            parameters = new List<QueryInfo>();
            var builder = new StringBuilder();
            var tableName = DotEntityDb.GetTableNameForType<T>();

            var whereString = "";

            if (where != null)
            {
                var whereStringBuilder = new List<string>();
                foreach (var wh in where)
                {
                    var parser = new ExpressionTreeParser(wh);
                    whereStringBuilder.Add(parser.GetWhereString());
                    var queryParameters = parser.QueryInfoList;
                    parameters = parameters.Concat(queryParameters).ToList();
                }
                whereString = string.Join(" AND ", whereStringBuilder).Trim();
            }

            var orderByStringBuilder = new List<string>();
            var orderByString = "";
            if (orderBy != null)
            {
                foreach (var ob in orderBy)
                {
                    var parser = new ExpressionTreeParser(ob.Key);
                    orderByStringBuilder.Add(parser.GetOrderByString() + (ob.Value == RowOrder.Descending ? " DESC" : ""));
                }

                orderByString = string.Join(", ", orderByStringBuilder).Trim(',');
            }
           
            // make the query now
            builder.Append($"SELECT * FROM ");
            builder.Append(tableName.ToEnclosed());

            if (!string.IsNullOrEmpty(whereString))
            {
                builder.Append(" WHERE " + whereString);
            }

            if (!string.IsNullOrEmpty(orderByString))
            {
                builder.Append(" ORDER BY " + orderByString);
            }
            if (page > 1 || count != int.MaxValue)
            {
                var offset = (page - 1) * count;
                builder.Append($" LIMIT {offset},{count}");
            }
            var query = builder.ToString().Trim() + ";";

            //and the count query
            query = query + $"{Environment.NewLine}SELECT COUNT(*) FROM {tableName.ToEnclosed()}" + (string.IsNullOrEmpty(whereString)
                        ? ""
                        : $" WHERE {whereString}") + ";";
            return query;
        }

        public override string GenerateJoin<T>(out IList<QueryInfo> parameters, List<IJoinMeta> joinMetas, List<LambdaExpression> @where = null, Dictionary<LambdaExpression, RowOrder> orderBy = null,
            int page = 1, int count = int.MaxValue)
        {
            parameters = new List<QueryInfo>();
            var typedAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var builder = new StringBuilder();
            var tableName = DotEntityDb.GetTableNameForType<T>();
            var parentAliasUsed = "t1";
            var lastAliasUsed = parentAliasUsed;
            typedAliases.Add(typeof(T).Name, lastAliasUsed);

            var joinBuilder = new StringBuilder();
            foreach (var joinMeta in joinMetas)
            {
                var joinedTableName = DotEntityDb.GetTableNameForType(joinMeta.OnType);
                if (!typedAliases.TryGetValue(joinedTableName, out string newAlias))
                {
                    newAlias = $"t{typedAliases.Count + 1}";
                }
                typedAliases.Add($"{joinedTableName}", newAlias);
                var sourceAlias = lastAliasUsed;
                if (joinMeta.SourceColumn == SourceColumn.Parent)
                {
                    sourceAlias = parentAliasUsed;
                }
                joinBuilder.Append(
                    $"{JoinMap[joinMeta.JoinType]} {joinedTableName.ToEnclosed()} {newAlias} ON {sourceAlias}.{joinMeta.SourceColumnName.ToEnclosed()} = {newAlias}.{joinMeta.DestinationColumnName.ToEnclosed()} ");

                lastAliasUsed = newAlias;

            }

            var whereStringBuilder = new List<string>();
            var whereString = "";

            if (where != null)
            {
                foreach (var wh in where)
                {
                    var parser = new ExpressionTreeParser(wh, typedAliases);
                    whereStringBuilder.Add(parser.GetWhereString());
                    var queryParameters = parser.QueryInfoList;
                    parameters = parameters.Concat(queryParameters).ToList();
                }
                whereString = string.Join(" AND ", whereStringBuilder).Trim();
            }

            var orderByStringBuilder = new List<string>();
            var orderByString = "";
            if (orderBy != null)
            {
                foreach (var ob in orderBy)
                {
                    var parser = new ExpressionTreeParser(ob.Key);
                    orderByStringBuilder.Add(parser.GetOrderByString() + (ob.Value == RowOrder.Descending ? " DESC" : ""));
                }

                orderByString = string.Join(", ", orderByStringBuilder).Trim(',');
            }
          
            // make the query now
            builder.Append($"SELECT * FROM ");

            builder.Append(tableName.ToEnclosed() + $" {parentAliasUsed} ");

            //join
            builder.Append(joinBuilder);

            if (!string.IsNullOrEmpty(whereString))
            {
                builder.Append(" WHERE " + whereString);
            }

            if (!string.IsNullOrEmpty(orderByString))
            {
                builder.Append(" ORDER BY " + orderByString);
            }
            if (page > 1 || count != int.MaxValue)
            {
                var offset = (page - 1) * count;
                builder.Append($" LIMIT {offset},{count}");
            }
            var query = builder.ToString().Trim() + ";";
            return query;
        }
    }
}
﻿// #region Author Information
// // QueryProcessor.cs
// // 
// // (c) Apexol Technologies. All Rights Reserved.
// // 
// #endregion

using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SpruceFramework
{
    internal class QueryProcessor : IQueryProcessor
    {
        public IDbCommand GetQueryCommand(IDbConnection connection, string sqlQuery, IList<QueryParameter> parameters, bool loadIdOfAffectedRow = false)
        {
            var command = connection.CreateCommand();
            command.CommandText = sqlQuery;
            foreach (var parameter in parameters.Where(x => !x.SupportOperator))
            {
                var cmdParameter = command.CreateParameter();
                cmdParameter.ParameterName = parameter.ParameterName;
                cmdParameter.Value = parameter.PropertyValue;
                command.Parameters.Add(cmdParameter);
            }
            if (loadIdOfAffectedRow)
            {
                //add an output parameter
                var idParameter = command.CreateParameter();
                idParameter.ParameterName = "Id";
                idParameter.Direction = ParameterDirection.Output;
                command.Parameters.Add(idParameter);
            }
            return command;
        }
    }
}
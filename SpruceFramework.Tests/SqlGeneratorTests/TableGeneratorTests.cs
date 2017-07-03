﻿// #region Author Information
// // TableGeneratorTests.cs
// // 
// // (c) Apexol Technologies. All Rights Reserved.
// // 
// #endregion

using NUnit.Framework;
using SpruceFramework.Tests.Data;

namespace SpruceFramework.Tests.SqlGeneratorTests
{
    [TestFixture]
    public class TableGeneratorTests
    {
        [Test]
        public void CreateTable_Succeeds()
        {
            var generator = new DefaultDatabaseTableGenerator();
            var sql = generator.GetCreateTableScript<ProductCategory>();
            var expected = @"CREATE TABLE [Product]
(	 [Id] INT NOT NULL,
	 [ProductName] NVARCHAR(MAX) NOT NULL,
	 [ProductDescription] NVARCHAR(MAX) NOT NULL,
	 [DateCreated] DATETIME NOT NULL,
	 [Price] NUMERIC(18,0) NOT NULL,
	 [IsActive] BIT NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC))
GO";
            Assert.AreEqual(expected, sql);
        }

        [Test]
        public void DropTable_Succeeds()
        {
            var generator = new DefaultDatabaseTableGenerator();
            var sql = generator.GetDropTableScript<Product>();
            var expected = @"DROP TABLE [Product]
GO";
            Assert.AreEqual(expected, sql);
        }

        [Test]
        public void CreateForeignKeyConstraint_Succeeds()
        {
            var relation = new Relation
            {
                SourceType = typeof(Product),
                DestinationType = typeof(ProductCategory),
                SourceColumnName = "Id",
                DestinationColumnName = "ProductId"
            };
            var generator = new DefaultDatabaseTableGenerator();
            var sql = generator.GetCreateConstraintScript(relation);
            var expected = @"ALTER TABLE [ProductCategory]
ADD CONSTRAINT FK_Product_Id_ProductCategory_ProductId
FOREIGN KEY ([ProductId]) REFERENCES [Product](Id);
GO";

            Assert.AreEqual(expected, sql);
        }

        [Test]
        public void DeleteForeignKeyConstraint_Succeeds()
        {
            var relation = new Relation
            {
                SourceType = typeof(Product),
                DestinationType = typeof(ProductCategory),
                SourceColumnName = "Id",
                DestinationColumnName = "ProductId"
            };
            var generator = new DefaultDatabaseTableGenerator();
            var sql = generator.GetDropConstraintScript(relation);
            var expected = @"ALTER TABLE [ProductCategory]
DROP CONSTRAINT FK_Product_Id_ProductCategory_ProductId;
GO";
            Assert.AreEqual(expected, sql);
        }
    }
}
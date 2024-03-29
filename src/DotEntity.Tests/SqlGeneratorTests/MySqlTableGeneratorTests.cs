﻿// #region Author Information
// // MySqlTableGeneratorTests.cs
// // 
// // (c) Sojatia Infocrafts Private Limited. All Rights Reserved.
// // 
// #endregion

using System;
using DotEntity.MySql;
using NUnit.Framework;
using DotEntity.Tests.Data;

namespace DotEntity.Tests.SqlGeneratorTests
{
    [TestFixture]
    public class MySqlTableGeneratorTests : DotEntityTest
    {
        private IDatabaseTableGenerator generator;
        [OneTimeSetUp]
        public void Init()
        {
            DotEntityDb.Initialize(MySqlConnectionString, new MySqlDatabaseProvider("mytest"));
            generator = DotEntityDb.Provider.DatabaseTableGenerator;
        }

        [Test]
        public void CreateTable_Succeeds()
        {
            var sql = generator.GetCreateTableScript<Product>();
            var expected = @"CREATE TABLE `Product`" + Environment.NewLine +
                           "(\t `Id` INT NOT NULL AUTO_INCREMENT," + Environment.NewLine +
                           "\t `ProductName` TEXT NOT NULL COLLATE utf8mb4_unicode_ci," + Environment.NewLine +
                           "\t `ProductDescription` TEXT NULL COLLATE utf8mb4_unicode_ci," + Environment.NewLine +
                           "\t `DateCreated` DATETIME NOT NULL," + Environment.NewLine +
                           "\t `Price` NUMERIC(18,5) NOT NULL," + Environment.NewLine +
                           "\t `IsActive` TINYINT(1) NOT NULL," + Environment.NewLine +
                           "PRIMARY KEY (`Id`));";
            Assert.AreEqual(expected, sql);
        }

        [Test]
        public void DropTable_Succeeds()
        {
            var sql = generator.GetDropTableScript<Product>();
            var expected = @"DROP TABLE IF EXISTS `Product`;";
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
            var sql = generator.GetCreateConstraintScript(relation);
            var expected = @"ALTER TABLE `ProductCategory`" + Environment.NewLine +
                           "ADD CONSTRAINT `cFK_Product_Id_ProductCategory_ProductId`" + Environment.NewLine +
                           "FOREIGN KEY `FK_Product_Id_ProductCategory_ProductId`(`ProductId`) REFERENCES `Product`(`Id`);";

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
            var sql = generator.GetDropConstraintScript(relation);
            var expected = @"ALTER TABLE `ProductCategory`" + Environment.NewLine + "DROP FOREIGN KEY `cFK_Product_Id_ProductCategory_ProductId`;";
            Assert.AreEqual(expected, sql);
        }

        [Test]
        public void CreateIndex_Succeeds()
        {
            var sql = generator.GetCreateIndexScript<Product>(new[] { nameof(Product.DateCreated) });
            var expected = "CREATE INDEX Idx_DateCreated ON `Product` (`DateCreated`)";
            Assert.AreEqual(expected, sql);
            sql = generator.GetCreateIndexScript<Product>(new[] {nameof(Product.DateCreated)}, null, true);
            expected = "CREATE UNIQUE INDEX Idx_DateCreated ON `Product` (`DateCreated`)";
            Assert.AreEqual(expected, sql);
        }

        [Test]
        public void DropIndex_Succeeds()
        {
            var sql = generator.GetDropIndexScript<Product>(new[] { nameof(Product.DateCreated) });
            var expected = $"ALTER TABLE `Product`{Environment.NewLine}DROP INDEX Idx_DateCreated;";
            Assert.AreEqual(expected, sql);

        }
    }
}
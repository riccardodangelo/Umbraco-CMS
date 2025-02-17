// Copyright (c) Umbraco.
// See LICENSE for more details.

using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Builders.Extensions;

namespace Umbraco.Cms.Tests.UnitTests.Umbraco.Infrastructure.Models
{
    [TestFixture]
    public class DataTypeTests
    {
        private DataTypeBuilder _builder;

        [SetUp]
        public void SetUp() => _builder = new DataTypeBuilder();

        [Test]
        public void Can_Deep_Clone()
        {
            DataType dtd = _builder
               .WithId(3123)
               .Build();

            var clone = (DataType)dtd.DeepClone();

            Assert.AreNotSame(clone, dtd);
            Assert.AreEqual(clone, dtd);
            Assert.AreEqual(clone.CreateDate, dtd.CreateDate);
            Assert.AreEqual(clone.CreatorId, dtd.CreatorId);
            Assert.AreEqual(clone.DatabaseType, dtd.DatabaseType);
            Assert.AreEqual(clone.Id, dtd.Id);
            Assert.AreEqual(clone.Key, dtd.Key);
            Assert.AreEqual(clone.Level, dtd.Level);
            Assert.AreEqual(clone.Name, dtd.Name);
            Assert.AreEqual(clone.ParentId, dtd.ParentId);
            Assert.AreEqual(clone.Path, dtd.Path);
            Assert.AreEqual(clone.SortOrder, dtd.SortOrder);
            Assert.AreEqual(clone.Trashed, dtd.Trashed);
            Assert.AreEqual(clone.UpdateDate, dtd.UpdateDate);

            // This double verifies by reflection
            PropertyInfo[] allProps = clone.GetType().GetProperties();
            foreach (PropertyInfo propertyInfo in allProps)
            {
                Assert.AreEqual(propertyInfo.GetValue(clone, null), propertyInfo.GetValue(dtd, null));
            }
        }

        [Test]
        public void Can_Serialize_Without_Error()
        {
            DataType item = _builder.Build();

            Assert.DoesNotThrow(() => JsonConvert.SerializeObject(item));
        }
    }
}

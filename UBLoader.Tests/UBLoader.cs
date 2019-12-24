using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UBLoader.Tests {
    [TestClass]
    public class UBLoader {
        [TestMethod]
        public void Can_Create_Instance() {
            var FC = new FilterCore();
            Assert.IsNotNull(FC);
        }
    }
}

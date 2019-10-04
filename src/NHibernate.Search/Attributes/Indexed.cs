using System;

namespace NHibernate.Search.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class IndexedAttribute : Attribute
    {
        private string index = string.Empty;
        private System.Type hibernateClass = null;

        public string Index
        {
            get { return index; }
            set { index = value; }
        }

        public System.Type HibernateClass
        {
            get { return hibernateClass; }
            set { hibernateClass = value; }
        }
    }
}
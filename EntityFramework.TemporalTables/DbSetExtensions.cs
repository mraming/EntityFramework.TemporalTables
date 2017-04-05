using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Reflection;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Mapping;

namespace EntityFramework.TemporalTables {
    public static class DbSetExtensions {
        public static IQueryable<TEntity> AsOf<TEntity>(this DbSet<TEntity> set, DateTime utcTimeStamp) where TEntity : class, ITemporalTable {
            ObjectParameter timestampParameter = new ObjectParameter("timestamp", utcTimeStamp);

            var objectContext = ((IObjectContextAdapter)set.getContext()).ObjectContext;

            var result = objectContext.CreateQuery<TEntity>($"eftt{GetTableName(typeof(TEntity), objectContext)}AsOf(@timestamp)", timestampParameter).AsQueryable();
            return result;
        }


        /// <summary>
        /// Helper method to get the <see cref="DbContext"/> instance to which a <see cref="DbSet{TEntity}"/> belongs.
        /// </summary>
        /// <remarks>
        /// HACK: This implementation makes use of reflection and detailed knowledge of the implementation details of
        /// internal and private scoped fields of the DbSet class.
        /// </remarks>
        private  static DbContext getContext<TEntity>(this DbSet<TEntity> dbSet) where TEntity : class {
            object internalSet = dbSet
                .GetType()
                .GetField("_internalSet", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(dbSet);
            object internalContext = internalSet
                .GetType()
                .BaseType
                .GetField("_internalContext", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(internalSet);
            return (DbContext)internalContext
                .GetType()
                .GetProperty("Owner", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(internalContext, null);
        }

        internal static string GetTableName(Type type, ObjectContext objectContext) {
            var metadata = objectContext.MetadataWorkspace;

            // Get the part of the model that contains info about the actual CLR types
            var objectItemCollection = ((ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace));

            // Get the entity type from the model that maps to the CLR type
            var entityType = metadata
                    .GetItems<EntityType>(DataSpace.OSpace)
                    .Single(e => objectItemCollection.GetClrType(e) == type);

            // Get the entity set that uses this entity type
            var entitySet = metadata
                .GetItems<EntityContainer>(DataSpace.CSpace)
                .Single()
                .EntitySets
                .Single(s => s.ElementType.Name == entityType.Name);

            // Find the mapping between conceptual and storage model for this entity set
            var mapping = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace)
                    .Single()
                    .EntitySetMappings
                    .Single(s => s.EntitySet == entitySet);

            // Find the storage entity set (table) that the entity is mapped
            var table = mapping
                .EntityTypeMappings.Single()
                .Fragments.Single()
                .StoreEntitySet;

            // Return the table name from the storage entity set
            return (string)table.MetadataProperties["Table"].Value ?? table.Name;
        }
    }
}

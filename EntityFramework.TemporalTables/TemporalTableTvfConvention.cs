
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Diagnostics;
using System.Linq;
using System.Data.Entity.Core.Objects;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using EntityFramework.TemporalTables.Utilities;
using System.Data.Entity.Infrastructure.Pluralization;

namespace EntityFramework.TemporalTables {

    /// <summary>
    /// EF Convention that adds the Table Valued Functions for each system versioned temporal table to the EDM StoreModel
    /// and ConceptualModel so that we can call them using Entity Sql from the <see cref="ObjectContext.CreateQuery"/>
    /// method
    /// </summary>
    internal class TemporalTableTvfConvention : IStoreModelConvention<EntityContainer> {
        private readonly string _defaultSchema;
        private static readonly IPluralizationService pluralizationService = (IPluralizationService)DbConfiguration.DependencyResolver.GetService(typeof(IPluralizationService), null);

        internal TemporalTableTvfConvention() {
            _defaultSchema = "dbo";
        }

        private IEnumerable<FunctionDescriptor> GetTemporalTableTvfFunctionDescriptions(DbModel model) {
            return model.ConceptualModel.EntityTypes.Where(t => TemporalTableConvention.IsTemporalTableType(t.Name)).Select(t =>
                new FunctionDescriptor(
                    getTvfName(t),
                    new List<ParameterDescriptor>() {
                        new ParameterDescriptor(
                            "timestamp",
                            PrimitiveType.GetEdmPrimitiveTypes().FirstOrDefault(p => p.ClrEquivalentType == typeof(DateTime)),
                            "datetime2",
                            false)
                    },
                    new EdmType[] { t },
                    null,
                    _defaultSchema, // TODO: Get the schema name from the entity as well - similar to table name
                    StoreFunctionKind.TableValuedFunction,
                    false));
        }

        public void Apply(EntityContainer item, DbModel model) {

            var functionDescriptors = GetTemporalTableTvfFunctionDescriptions(model);

            var storeFunctionBuilder = new StoreFunctionBuilder(model, _defaultSchema, "CodeFirstDatabaseSchema");

            foreach (var functionDescriptor in functionDescriptors) {
                var storeFunctionDefinition = storeFunctionBuilder.Create(functionDescriptor);
                model.StoreModel.AddItem(storeFunctionDefinition);

                if (functionDescriptor.StoreFunctionKind != StoreFunctionKind.ScalarUserDefinedFunction) {
                    var functionImportDefinition = CreateFunctionImport(model, functionDescriptor);
                    model.ConceptualModel.Container.AddFunctionImport(functionImportDefinition);

                    if (functionImportDefinition.IsComposableAttribute) {
                        model.ConceptualToStoreMapping.AddFunctionImportMapping(
                            new FunctionImportMappingComposable(
                                functionImportDefinition,
                                storeFunctionDefinition,
                                new FunctionImportResultMapping(),
                                model.ConceptualToStoreMapping));
                    } else {
                        model.ConceptualToStoreMapping.AddFunctionImportMapping(
                            new FunctionImportMappingNonComposable(
                                functionImportDefinition,
                                storeFunctionDefinition,
                                new FunctionImportResultMapping[0],
                                model.ConceptualToStoreMapping));
                    }
                }
            }
        }

        private static EdmFunction CreateFunctionImport(DbModel model, FunctionDescriptor functionImport) {
            EntitySet[] entitySets;
            FunctionParameter[] returnParameters;
            CreateReturnParameters(model, functionImport, out returnParameters, out entitySets);

            var functionPayload =
                new EdmFunctionPayload {
                    Parameters =
                        functionImport
                            .Parameters
                            .Select(
                                p => FunctionParameter.Create(p.Name, p.EdmType,
                                        p.IsOutParam ? ParameterMode.InOut : ParameterMode.In))
                            .ToList(),
                    ReturnParameters = returnParameters,
                    IsComposable = functionImport.StoreFunctionKind == StoreFunctionKind.TableValuedFunction,
                    IsFunctionImport = true,
                    EntitySets = entitySets
                };

            return EdmFunction.Create(
                functionImport.Name,
                model.ConceptualModel.Container.Name,
                DataSpace.CSpace,
                functionPayload,
                null);
        }

        private static void CreateReturnParameters(DbModel model, FunctionDescriptor functionImport,
            out FunctionParameter[] returnParameters, out EntitySet[] entitySets) {
            var resultCount = functionImport.ReturnTypes.Count();
            entitySets = new EntitySet[resultCount];
            returnParameters = new FunctionParameter[resultCount];

            for (int i = 0; i < resultCount; i++) {
                var returnType = functionImport.ReturnTypes[i];

                if (returnType.BuiltInTypeKind == BuiltInTypeKind.EntityType) {
                    var types = Tools.GetTypeHierarchy(returnType);

                    var matchingEntitySets =
                        model.ConceptualModel.Container.EntitySets
                            .Where(s => types.Contains(s.ElementType))
                            .ToArray();

                    if (matchingEntitySets.Length == 0) {
                        throw new InvalidOperationException(
                            string.Format(
                                "The model does not contain EntitySet for the '{0}' entity type.",
                                returnType.FullName));
                    }

                    Debug.Assert(matchingEntitySets.Length == 1, "Invalid model (MEST)");

                    entitySets[i] = matchingEntitySets[0];
                }

                returnParameters[i] = FunctionParameter.Create(
                    "ReturnParam" + i,
                    returnType.GetCollectionType(),
                    ParameterMode.ReturnValue);
            }
        }

        private string getTvfName(EdmType entityType) {
            string tableName;
            if (entityType.MetadataProperties.TryGetValue("TableName", false, out var property)) {
                tableName = DatabaseName.Parse(property.Value.ToString()).Name;
            } else {
                // HACK: Think this mimics the EF default naming of tables.
                tableName = pluralizationService.Pluralize(entityType.Name);
            }
            return $"eftt{tableName}AsOf";
        }
    }
}

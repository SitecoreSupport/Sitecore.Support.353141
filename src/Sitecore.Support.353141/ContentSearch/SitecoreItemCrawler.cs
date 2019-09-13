using System;
using System.Reflection;
using System.Threading;
using Sitecore.ContentSearch;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;

namespace Sitecore.Support.ContentSearch
{
    public class SitecoreItemCrawler : Sitecore.ContentSearch.SitecoreItemCrawler
    {
        private static readonly MethodInfo IsRootOrDescendantMethodInfo =
          typeof(Sitecore.ContentSearch.SitecoreItemCrawler).GetMethod("IsRootOrDescendant",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdatePreviousVersionMethodInfo =
          typeof(Sitecore.ContentSearch.SitecoreItemCrawler).GetMethod("UpdatePreviousVersion",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public override void Update(IProviderUpdateContext context, IIndexableUniqueId indexableUniqueId, IndexEntryOperationContext operationContext, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            Assert.ArgumentNotNull(indexableUniqueId, "indexableUniqueId");

            var contextEx = context as ITrackingIndexingContext;
            var skipIndexable = contextEx != null && !contextEx.Processed.TryAdd(indexableUniqueId, null);

            if (skipIndexable || !ShouldStartIndexing(indexingOptions))
                return;

            var options = this.DocumentOptions;
            Assert.IsNotNull(options, "DocumentOptions");

            if (this.IsExcludedFromIndex(indexableUniqueId, operationContext, true))
                return;

            if (operationContext != null)
            {
                if (operationContext.NeedUpdateChildren)
                {
                    var item = Data.Database.GetItem(indexableUniqueId as SitecoreItemUniqueId);

                    if (item != null)
                    {
                        // check if we moved item out of the index's root.
                        bool needDelete = operationContext.OldParentId != Guid.Empty
                               && this.IsRootOrDescendant(new ID(operationContext.OldParentId))
                               && !this.IsAncestorOf(item);

                        if (needDelete)
                        {
                            this.Delete(context, indexableUniqueId);
                            return;
                        }

                        this.UpdateHierarchicalRecursive(context, item, CancellationToken.None);
                        return;
                    }
                }

                if (operationContext.NeedUpdatePreviousVersion)
                {
                    var item = Data.Database.GetItem(indexableUniqueId as SitecoreItemUniqueId);
                    if (item != null)
                    {
                        this.UpdatePreviousVersion(item, context);
                    }
                }
            }

            var indexable = this.GetIndexableAndCheckDeletes(indexableUniqueId);

            if (indexable == null)
            {
                if (this.GroupShouldBeDeleted(indexableUniqueId.GroupId))
                {
                    this.Delete(context, indexableUniqueId.GroupId);
                    return;
                }

                this.Delete(context, indexableUniqueId);
                return;
            }

            this.DoUpdate(context, indexable, operationContext);
        }

        private void UpdatePreviousVersion(Item item, IProviderUpdateContext context)
        {
            UpdatePreviousVersionMethodInfo.Invoke(this, new object[] { item, context });
        }

        private bool IsRootOrDescendant(ID iD)
        {
            return (bool)IsRootOrDescendantMethodInfo.Invoke(this, new object[] { iD });
        }
    }
}
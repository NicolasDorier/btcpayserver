#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using Amazon.S3.Model;
using BTCPayServer.Client.Models;
using BTCPayServer.Services.Reporting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Serilog.Events;
using TwentyTwenty.Storage;
using static BTCPayServer.Models.DataTableViewModel.DataRowViewModel;

namespace BTCPayServer.Models
{
    public class DataTableViewModel
    {
        public List<DataRowViewModel> Rows { get; }


        public class DataRowViewModel
        {
            public record DataCellViewModel(object? Value = null, int RowSpan = 1, int ColSpan = 1);
            public List<DataCellViewModel> Cells { get; internal set; } = new List<DataCellViewModel>();
        }
        public DataTableViewModel(
            ChartDefinition chartDefinition,
            IList<StoreReportResponse.Field> fields,
            IList<IList<object?>> rows)
        {
            var fieldNames = fields.Select(f => f.Name).ToArray();
            var aggregateNames = chartDefinition.Aggregates.ToArray();
            var groupNames = chartDefinition.Groups.ToArray();
            var groupIndices = groupNames.Select(g => Array.IndexOf(fieldNames, g)).Where((i, ind) => i != -1 || ThrowNotFound(groupNames, ind)).ToArray();
            var aggregatesIndices = aggregateNames.Select(g => Array.IndexOf(fieldNames, g)).Where((i, ind) => i != -1 || ThrowNotFound(aggregateNames, ind)).ToArray();
            var totalLevels = chartDefinition.Totals.Select(g => chartDefinition.Groups.IndexOf(g) + 1).Where((i, ind) => i != 0 || ThrowNotFound(chartDefinition.Totals, ind)).ToArray();


            // Filter rows
            var rowsArr = ApplyFilters(rows, fields, chartDefinition.Filters);



            // Sort by group columns
            ((List<IList<object?>>)rowsArr).Sort(ByColumns(groupIndices));

            // Group data represent tabular data of all the groups and aggregates given the data.
            // [Region, Crypto, PaymentType]

            // There will be several level of aggregation
            // For example, if you have 3 groups: [Region, Crypto, PaymentType] then you have 4 group data.
            // [Region, Crypto, PaymentType]
            // [Region, Crypto]
            // [Region]
            // []
            List<IList<IList<object?>>> groupLevels = new();
            do
            {
                (fields, rowsArr) = GroupBy(groupIndices, aggregatesIndices, fields, rowsArr);
                groupLevels.Add(rowsArr);
                if (groupIndices.Length == 0)
                    break;

                // We are grouping the group data.
                // For our example of 2 groups and 2 aggregate, then:
                // First iteration: groupIndices = [0, 1], aggregateIndices = [3, 4]
                // Second iteration: groupIndices = [0], aggregateIndices = [2, 3]
                // Last iteration: groupIndices = [], aggregateIndices = [1, 2]

                groupIndices = new int[groupIndices.Length - 1];
                for (int i = 0; i < groupIndices.Length; i++)
                {
                    groupIndices[i] = i;
                }

                aggregatesIndices = new int[aggregatesIndices.Length];
                for (var ai = 0; ai < aggregatesIndices.Length; ai++)
                {
                    aggregatesIndices[ai] = groupIndices.Length + 1 + ai;
                }
            } while (true);

            // Put the highest level ([]) on top
            groupLevels.Reverse();

            // Make a tree of the groups
            var root = new TreeNode(
                Parent: null,
                Groups: new object?[0],
                // Note that the top group data always have one row aggregating all
                Values: chartDefinition.HasGrandTotal ? groupLevels[0][0] : new List<object?>(),
                Children: new List<TreeNode>(),
                Level: 0,
                RLevel: groupLevels.Count
                );

            // Build the tree
            MakeTree(root, groupLevels, totalLevels);

            // Add a leafCount property to each node, it is the number of leaf below each nodes.
            VisitTree(root);

            // Create a representation that can easily be binded to VueJS
            var rowsVm = new List<DataRowViewModel>();
            BuildRows(root, rowsVm);
            Rows = rowsVm;
        }

        private bool ThrowNotFound(IList<string> names, int ind)
        {
            throw new KeyNotFoundException($"The field '{names[ind]}' is not found");
        }

        private void BuildRows(TreeNode node, IList<DataRowViewModel> rows)
        {
            if (node.Children.Count == 0 && node.Level != 0)
            {
                var row = new DataRowViewModel();

                if (!node.IsTotal)
                    row.Cells.Add(new DataCellViewModel(node.Groups[node.Groups.Count - 1]));
                else
                    row.Cells.Add(new DataCellViewModel("Total", ColSpan: node.RLevel));

                var parent = node.Parent;
                var n = node;
                while (parent != null && parent.Level != 0 && parent.Children[0] == n)
                {
                    row.Cells.Add(new DataCellViewModel(parent.Groups[parent.Groups.Count - 1], parent.LeafCount));
                    n = parent;
                    parent = parent.Parent;
                }
                row.Cells.Reverse();
                row.Cells.AddRange(node.Values.Select(v => new DataCellViewModel(v)));
                rows.Add(row);
            }
            foreach (var child in node.Children)
            {
                BuildRows(child, rows);
            }
            if (node.Parent == null && node.Values.Count > 0)
            {
                var row = new DataRowViewModel();
                row.Cells.Add(new DataCellViewModel("Grand total", ColSpan: node.RLevel - 1));
                row.Cells.AddRange(node.Values.Select(v => new DataCellViewModel(v)));
                rows.Add(row);
            }
        }

        // Add a leafCount property, the number of leaf below each nodes
        // Remove total if there is only one child outside of the total
        private void VisitTree(TreeNode node)
        {
            node.LeafCount = 0;
            if (node.Children.Count == 0)
            {
                node.LeafCount++;
                return;
            }
            for (var i = 0; i < node.Children.Count; i++)
            {
                VisitTree(node.Children[i]);
                node.LeafCount += node.Children[i].LeafCount;
            }
        }

        private void MakeTree(TreeNode parent, List<IList<IList<object?>>> groupLevels, int[] totalLevels)
        {
            var level = parent.Level + 1;
            var rlevel = parent.RLevel - 1;

            if (Array.IndexOf(totalLevels, level - 1) != -1)
            {
                parent.Children.Add(new TreeNode(
                    Parent: parent,
                    Groups: parent.Groups,
                    Values: parent.Values,
                    Children: new List<TreeNode>(),
                    Level: level,
                    RLevel: rlevel,
                    IsTotal: true
                    ));
            }

            for (var i = 0; i < groupLevels[level].Count; i++)
            {
                var groupData = groupLevels[level][i];
                for (var gi = 0; gi < parent.Groups.Count; gi++)
                {
                    if (Comparer.DefaultInvariant.Compare(parent.Groups[gi], groupData[gi]) != 0)
                    {
                        goto nextRow;
                    }
                }
                // This row conforms to the parent
                var node = new TreeNode(
                    Parent: parent,
                    Groups: groupData.Take(level).ToArray(),
                    Values: groupData.Skip(level).ToArray(),
                    Children: new List<TreeNode>(),
                    Level: level,
                    RLevel: rlevel
                    );
                parent.Children.Add(node);
                if (groupLevels.Count > level + 1)
                {
                    MakeTree(node, groupLevels, totalLevels);
                }
nextRow:
                ;
            }
        }

        record TreeNode(
            TreeNode? Parent,
            IList<object?> Groups,
            IList<object?> Values,
            IList<TreeNode> Children,
            // level=0 means the root, it increments 1 each level
            int Level,
            // rlevel is the reverse. It starts from the highest level and goes down to 0
            int RLevel,
            bool IsTotal = false
            )
        {
            public int LeafCount { get; set; }
        }


        // Given sorted data, build a tabular data of given groups and aggregates.
        public (IList<StoreReportResponse.Field> Fields, IList<IList<object?>> Rows)
            GroupBy(
            IList<int> groupIndices,
            IList<int> aggregatesIndices,
            IList<StoreReportResponse.Field> fields,
            IList<IList<object?>> rows)
        {
            var summaryFields = new List<StoreReportResponse.Field>();
            var summaryRows = new List<IList<object?>>();
            var aggregateFunctions = aggregatesIndices.Select(i => fields[i].DefaultAggregateFunction).ToArray();

            foreach (var gi in groupIndices)
            {
                summaryFields.Add(fields[gi]);
            }
            foreach (var ai in aggregatesIndices)
            {
                summaryFields.Add(fields[ai]);
            }

            object?[]? summaryRow = null;
            for (var i = 0; i < rows.Count; i++)
            {
                if (summaryRow is not null)
                {
                    for (var gi = 0; gi < groupIndices.Count; gi++)
                    {
                        if (Comparer.Default.Compare(summaryRow[gi], rows[i][groupIndices[gi]]) != 0)
                        {
                            summaryRows.Add(summaryRow);
                            summaryRow = null;
                            break;
                        }
                    }
                }
                if (summaryRow is null)
                {
                    summaryRow = new object[groupIndices.Count + aggregatesIndices.Count];
                    for (var gi = 0; gi < groupIndices.Count; gi++)
                    {
                        summaryRow[gi] = rows[i][groupIndices[gi]];
                    }
                }
                for (var ai = 0; ai < aggregatesIndices.Count; ai++)
                {
                    var v = rows[i][aggregatesIndices[ai]];
                    var currentValue = summaryRow[groupIndices.Count + ai] ?? aggregateFunctions[ai].Seed;
                    if (v is not null)
                        summaryRow[groupIndices.Count + ai] = aggregateFunctions[ai].Aggregate(currentValue, v);

                }
            }
            if (summaryRow is not null)
            {
                summaryRows.Add(summaryRow);
            }
            return (summaryFields, summaryRows);
        }

        private Comparison<IList<object?>> ByColumns(int[] columnIndices)
        {
            return (a, b) =>
            {
                for (var i = 0; i < columnIndices.Length; i++)
                {
                    var fieldIndex = columnIndices[i];
                    var res = Comparer.DefaultInvariant.Compare(a[fieldIndex], b[fieldIndex]);
                    if (res != 0)
                        return res;
                }
                return 0;
            };
        }

        private IList<IList<object?>> ApplyFilters(IList<IList<object?>> rows, IList<StoreReportResponse.Field> fields, List<string>? filters)
        {
            if (fields is null || fields.Count == 0)
                return rows.ToList();
            return rows.ToList();
        }
    }
}

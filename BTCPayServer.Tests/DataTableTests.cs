using System.Collections.Generic;
using System;
using Xunit;
using BTCPayServer.Models;
using BTCPayServer.Client.Models;
using System.Linq;

namespace BTCPayServer.Tests
{
    [Trait("Fast", "Fast")]
    public class DataTableTests
    {
        List<StoreReportResponse.Field> GetFields()
        {
            return new List<StoreReportResponse.Field>()
            {
                new StoreReportResponse.Field("Color", "string"),
                new StoreReportResponse.Field("Size", "string"),
                new StoreReportResponse.Field("Price", "decimal"),
            };
        }
        List<IList<object>> GetData()
        {
            List<IList<object>> rows = new List<IList<object>>
            {
                new List<object> { "Red", "Small", 12.50m },
                new List<object> { "Blue", "Medium", 45.00m },
                new List<object> { "Green", "Large", 80.99m },
                new List<object> { "Red", "Medium", 35.75m },
                new List<object> { "Blue", "Large", 65.30m },
                new List<object> { "Green", "Small", 15.89m },
                new List<object> { "Red", "Large", 90.20m },
                new List<object> { "Blue", "Small", 18.60m },
                new List<object> { "Green", "Medium", 55.55m },
                new List<object> { "Red", "Small", 22.25m },
                new List<object> { "Blue", "Medium", 40.40m },
                new List<object> { "Green", "Large", 78.78m },
                new List<object> { "Red", "Medium", 33.33m },
                new List<object> { "Blue", "Large", 67.67m },
                new List<object> { "Green", "Small", 19.19m },
                new List<object> { "Red", "Large", 88.88m },
                new List<object> { "Blue", "Small", 23.23m },
                new List<object> { "Green", "Medium", 50.50m },
                new List<object> { "Red", "Small", 10.10m },
                new List<object> { "Blue", "Medium", 44.44m }
            };
            return rows;
        }
        string GetCSV(List<IList<object>> rows, IList<StoreReportResponse.Field> fields)
        {
            var data =
                string.Join("\r\n", rows
                .Select(row => String.Join(",", row)));
            data = string.Join(",", fields.Select(f => f.Name)) + "\r\n" +  data;
            return data;
        }

        [Fact]
        public void CanGenerateDataTable()
        {
            var model = new DataTableViewModel(new Client.Models.ChartDefinition()
            {
                Groups = new List<string>() { "Color" },
                Aggregates = new List<string>() { "Price" }
            },
            GetFields(),
            GetData());

            AssertLine(model, 0, "Blue,304.64");
            AssertLine(model, 1, "Green,300.90");
            AssertLine(model, 2, "Red,293.01");

            foreach (var hasGrandTotal in new[] { true, false })
                foreach (var hasTotal in new[] { true, false })
                {
                    model = new DataTableViewModel(new Client.Models.ChartDefinition()
                    {
                        Groups = new List<string>() { "Color", "Size" },
                        Aggregates = new List<string>() { "Price" },
                        Totals = hasTotal ? new List<string>() { "Color" } : new List<string>(),
                        HasGrandTotal = hasGrandTotal
                    },
                    GetFields(),
                    GetData());

                    int i = 0;

                    if (hasTotal)
                    {
                        AssertLine(model, i++, "Blue(4R),Total,304.64");
                        AssertLine(model, i++, "Large,132.97");
                    }
                    else
                    {
                        AssertLine(model, i++, "Blue(3R),Large,132.97");
                    }
                    AssertLine(model, i++, "Medium,129.84");
                    AssertLine(model, i++, "Small,41.83");

                    if (hasTotal)
                    {
                        AssertLine(model, i++, "Green(4R),Total,300.90");
                        AssertLine(model, i++, "Large,159.77");
                    }
                    else
                    {
                        AssertLine(model, i++, "Green(3R),Large,159.77");
                    }
                    AssertLine(model, i++, "Medium,106.05");
                    AssertLine(model, i++, "Small,35.08");

                    if (hasTotal)
                    {
                        AssertLine(model, i++, "Red(4R),Total,293.01");
                        AssertLine(model, i++, "Large,179.08");
                    }
                    else
                    {
                        AssertLine(model, i++, "Red(3R),Large,179.08");
                    }
                    AssertLine(model, i++, "Medium,69.08");
                    AssertLine(model, i++, "Small,44.85");
                    if (hasGrandTotal)
                        AssertLine(model, i++, "Grand total(2C),898.55");
                    else
                        Assert.Throws<IndexOutOfRangeException>(() => AssertLine(model, i++, "Grand total,898.55"));
                }
        }

        [Fact]
        public void CanHaveMultipleTotals()
        {
            var data = new List<IList<object>>()
           {
                new List<object> { "A", "USD", "On", "Car", 4.0m, 13m },
                new List<object> { "A", "USD", "On", "Car", 4.0m, 13m },
                new List<object> { "A", "USD", "On", "Car", 4.0m, 13m },
                new List<object> { "A", "USD", "On", "Bike", 1.0m, 13m },
                new List<object> { "A", "USD", "On", "Bike", 1.0m, 13m },
                new List<object> { "A", "USD", "Off", "Bike", 1.0m, 13m }
           };
            var fields = new List<StoreReportResponse.Field>()
            {
                new StoreReportResponse.Field("AppId", "string"),
                new StoreReportResponse.Field("Currency", "string"),
                new StoreReportResponse.Field("State", "string"),
                new StoreReportResponse.Field("Product", "string"),
                new StoreReportResponse.Field("Quantity", "decimal"),
                new StoreReportResponse.Field("CurrencyAmount", "decimal"),
            };
            var model = new DataTableViewModel(new Client.Models.ChartDefinition()
            {
                Groups = new List<string>() { "AppId", "Currency", "State", "Product" },
                Aggregates = new List<string>() { "Quantity", "CurrencyAmount" },
                Totals = new List<string>() { "AppId", "Currency", "State", "Product" },
            },
           fields,
           data);
            
            int i = 0;

            AssertLine(model, i++, "A(7R),Total(3C),15.0,78");
            AssertLine(model, i++, "USD(6R),Total(2C),15.0,78");
            AssertLine(model, i++, "Off(2R),Total,1.0,13");
            AssertLine(model, i++, "Bike,1.0,13");
            AssertLine(model, i++, "On(3R),Total,14.0,65");
            AssertLine(model, i++, "Bike,2.0,26");
            AssertLine(model, i++, "Car,12.0,39");
        }

        private void AssertLine(DataTableViewModel model, int line, string expected)
        {
            var modelString = ToString(model);
            var splitted = modelString.Split("\r\n");
            Assert.Equal(expected, splitted[line]);
        }
        private string ToString(DataTableViewModel model)
        {
            return string.Join("\r\n", model.Rows.Select(r => ToString(r)));
        }
        private string ToString(DataTableViewModel.DataRowViewModel actualRow)
        {
            List<string> fields = new List<string>();
            foreach (var g in actualRow.Cells)
            {
                var field = g.Value?.ToString() ?? "<NULL>";
                if (g.RowSpan != 1)
                    field += $"({g.RowSpan}R)";
                if (g.ColSpan != 1)
                    field += $"({g.ColSpan}C)";
                fields.Add(field);
            }
            return string.Join(",", fields);
        }
    }
}

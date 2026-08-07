using POTracker.Services;

namespace POTracker.UnitTests
{
    public sealed class WegPurchaseOrderHeaderParserTests
    {
        [Theory]
        [InlineData("4517615187", "Ranjithkumar G", "ranjithkumar@weg.net")]
        [InlineData("4517675794", "Chandrashekara P", "ravindran@weg.net")]
        [InlineData("4517860593", "Vishal S K", "e-vishalsk@weg.net")]
        [InlineData("4517865689", "Nagappan P L", "plnagappan@weg.net")]
        [InlineData("4517902655", "Gokulnath V", "gokulnathv@weg.net")]
        [InlineData("4517913686", "Abdul", "abduli@weg.net")]
        [InlineData("4517917168", "Gokulnath V", "gokulnathv@weg.net")]
        [InlineData("4517943688", "Gokulnath V", "gokulnathv@weg.net")]
        [InlineData("4517979275", "Ranjithkumar G", "ranjithkumar@weg.net")]
        [InlineData("4517990579", "Gokulnath V", "gokulnathv@weg.net")]
        [InlineData("4517998076", "Sivagnanam B", "sivagnanamb@weg.net")]
        [InlineData("4518014585", "Chandrashekara P", "ravindran@weg.net")]
        [InlineData("4518052687", "Abdul", "abduli@weg.net")]
        [InlineData("4518058774", "Chandrashekara P", "ravindran@weg.net")]
        [InlineData("4518063179", "Sivagnanam B", "sivagnanamb@weg.net")]
        [InlineData("4518083769", "Gokulnath V", "gokulnathv@weg.net")]
        [InlineData("4518096950", "Ranjithkumar G", "ranjithkumar@weg.net")]
        [InlineData("4518100987", "Paramaguru Siva S", "paramagurus@weg.net")]
        [InlineData("4518103227", "Chandrashekara P", "ravindran@weg.net")]
        [InlineData("4518118987", "Vishal S K", "e-vishalsk@weg.net")]
        public void ExtractPurchaserName_ReturnsWegCompany_NotBuyerOrVendor(
            string purchaseOrderNumber,
            string buyerContact,
            string buyerEmail)
        {
            var extractedText =
                $"""
                Vendor: 34692 INDHRA TURNING POINT
                01 VASANTHAM NAGAR, HOSUR
                Order Nº: {purchaseOrderNumber}
                Quotation:
                {buyerContact}
                {buyerEmail}
                04344 301584
                Weg Industries (India) PVT LTD
                PLOT NO. E-20 (NORTH) SIPCOT INDL.
                HOSUR - 635109, TN
                Item Material Quantity Unit
                """;

            var purchaserName =
                WegPurchaseOrderHeaderParser
                    .ExtractPurchaserName(
                        extractedText);

            Assert.Equal(
                WegPurchaseOrderHeaderParser
                    .CanonicalWegPurchaserName,
                purchaserName);

            Assert.NotEqual(
                buyerContact,
                purchaserName);

            Assert.NotEqual(
                "INDHRA TURNING POINT",
                purchaserName);
        }

        [Theory]
        [InlineData("Weg Industries (India) PVT LTD")]
        [InlineData("WEG INDUSTRIES (INDIA) PVT. LTD.")]
        [InlineData("WEG Industries ( India ) Private Limited")]
        public void NormalizePurchaserName_NormalizesWegVariants(
            string value)
        {
            Assert.Equal(
                WegPurchaseOrderHeaderParser
                    .CanonicalWegPurchaserName,
                WegPurchaseOrderHeaderParser
                    .NormalizePurchaserName(value));
        }

        [Fact]
        public void ExtractPurchaserName_DoesNotUseVendorWhenPurchaserIsMissing()
        {
            const string extractedText =
                """
                Vendor: 34692 INDHRA TURNING POINT
                Order Nº: 4517000000
                Ranjithkumar G
                ranjithkumar@weg.net
                Item Material Quantity Unit
                """;

            Assert.Equal(
                string.Empty,
                WegPurchaseOrderHeaderParser
                    .ExtractPurchaserName(
                        extractedText));
        }

        [Fact]
        public void ExtractPurchaserName_SupportsGenericPurchaserCompany()
        {
            const string extractedText =
                """
                Vendor: 10001 SAMPLE SUPPLIER
                Order Nº: 5500000001
                Buyer Name
                buyer@example.com
                Example Motors Private Limited
                Industrial Area
                Item Material Quantity Unit
                """;

            Assert.Equal(
                "Example Motors Private Limited",
                WegPurchaseOrderHeaderParser
                    .ExtractPurchaserName(
                        extractedText));
        }

        [Fact]
        public void CreateComparisonKey_IgnoresCaseSpacesAndPunctuation()
        {
            var firstKey =
                WegPurchaseOrderHeaderParser
                    .CreateComparisonKey(
                        "WEG Industries (India) PVT LTD");

            var secondKey =
                WegPurchaseOrderHeaderParser
                    .CreateComparisonKey(
                        "weg industries india pvt. ltd.");

            Assert.Equal(
                firstKey,
                secondKey);
        }
    }
}
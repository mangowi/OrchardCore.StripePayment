using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace OrchardCore.StripePayment
{
    public class StripePaymentMigrations : DataMigration
    {
        private readonly IContentDefinitionManager _contentDefinitionManager;

        public StripePaymentMigrations(IContentDefinitionManager contentDefinitionManager)
        {
            _contentDefinitionManager = contentDefinitionManager;
        }

        public int Create()
        {
            _contentDefinitionManager.AlterTypeDefinition("StripePaymentForm", builder => builder
                .Draftable()
                .Versionable()
                .Listable()
                .WithPart("PaymentPart", part => part.WithPosition("1"))
            );

            return 1;
        }
    }
}
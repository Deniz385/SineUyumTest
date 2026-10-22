using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace SineUyum.Api.Localization
{
    // Validation messages that do not come from our own DTO keys: attributes
    // without an ErrorMessage (including MVC's implicit [Required] for
    // non-nullable strings and [EnumDataType]), model binding errors and
    // unreadable JSON. Field names are localized too ("fields.recipientId"),
    // so "{0} alanı zorunludur." reads "Alıcı alanı zorunludur."
    public sealed class LocalizedValidationMetadataProvider : IValidationMetadataProvider, IDisplayMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (attribute.ErrorMessage != null || attribute.ErrorMessageResourceType != null) continue;
                attribute.ErrorMessage = attribute switch
                {
                    // The body parameter itself (missing or unreadable body).
                    RequiredAttribute when context.Key.MetadataKind == ModelMetadataKind.Parameter => "validation.bodyRequired",
                    RequiredAttribute => "validation.required",
                    MaxLengthAttribute => "validation.maxLength",
                    StringLengthAttribute => "validation.maxLength",
                    MinLengthAttribute => "validation.minLength",
                    RangeAttribute => "validation.range",
                    EmailAddressAttribute => "validation.email",
                    _ => "validation.invalid"
                };
            }
        }

        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            if (context.Key.MetadataKind != ModelMetadataKind.Property || context.DisplayMetadata.DisplayName != null) return;
            var name = context.Key.Name!;
            // Evaluated per message, so it follows the request language.
            context.DisplayMetadata.DisplayName = () => FieldName(name);
        }

        // "RecipientId" -> fields.recipientId in the request language; the
        // property name when there is no entry.
        public static string FieldName(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return string.Empty;
            var key = "fields." + char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
            return LocaleCatalog.Shared.TryGetTemplate(SupportedLanguages.Current, key, out var name) ? name : propertyName;
        }
    }

    // MVC localizes only the attributes it has an adapter for ([Required],
    // [Range], ...); others such as [EnumDataType] used to return the raw key.
    // This generic adapter localizes them the same way.
    public sealed class LocalizedAttributeAdapterProvider : IValidationAttributeAdapterProvider
    {
        private readonly ValidationAttributeAdapterProvider _inner = new();

        public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer) =>
            _inner.GetAttributeAdapter(attribute, stringLocalizer) ?? new GenericAttributeAdapter(attribute, stringLocalizer);

        private sealed class GenericAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer)
            : AttributeAdapterBase<ValidationAttribute>(attribute, stringLocalizer)
        {
            // Server-side only; no client-side (HTML) validation attributes.
            public override void AddValidation(ClientModelValidationContext context) { }

            public override string GetErrorMessage(ModelValidationContextBase validationContext) =>
                GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
        }
    }

    public static class ValidationLocalization
    {
        private static string T(string key, params object?[] args) =>
            LocaleCatalog.Shared.Translate(SupportedLanguages.Current, key, args);

        public static void Configure(MvcOptions options)
        {
            options.ModelMetadataDetailsProviders.Add(new LocalizedValidationMetadataProvider());

            var messages = options.ModelBindingMessageProvider;
            var field = LocalizedValidationMetadataProvider.FieldName;
            messages.SetMissingBindRequiredValueAccessor(name => T("validation.required", field(name)));
            messages.SetMissingKeyOrValueAccessor(() => T("validation.valueRequired"));
            messages.SetMissingRequestBodyRequiredValueAccessor(() => T("validation.bodyRequired"));
            messages.SetValueMustNotBeNullAccessor(_ => T("validation.valueRequired"));
            messages.SetAttemptedValueIsInvalidAccessor((value, name) => T("validation.invalid", field(name)));
            messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => T("validation.invalidValue"));
            messages.SetUnknownValueIsInvalidAccessor(name => T("validation.invalid", field(name)));
            messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => T("validation.invalidValue"));
            messages.SetValueIsInvalidAccessor(_ => T("validation.invalidValue"));
            messages.SetValueMustBeANumberAccessor(name => T("validation.mustBeNumber", field(name)));
            messages.SetNonPropertyValueMustBeANumberAccessor(() => T("validation.invalidValue"));
        }

        // Unreadable JSON is reported under its JSON path ("$.movieId") with a
        // fixed English text; replace it with "Movie has an invalid value."
        public static void Configure(ApiBehaviorOptions options)
        {
            var inner = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
            {
                var unreadable = context.ModelState.Keys.Any(k => k.StartsWith('$'));
                var bodyRequired = T("validation.bodyRequired");
                foreach (var (key, entry) in context.ModelState)
                {
                    // A body that was sent but could not be read is not "missing".
                    if (unreadable && entry.Errors.All(e => e.ErrorMessage == bodyRequired))
                    {
                        entry.Errors.Clear();
                        continue;
                    }
                    if (!key.StartsWith('$') || entry.Errors.Count == 0) continue;
                    var property = key.Split('.').Last().Split('[')[0];
                    entry.Errors.Clear();
                    entry.Errors.Add(property is "" or "$"
                        ? T("validation.invalidValue")
                        : T("validation.invalid", LocalizedValidationMetadataProvider.FieldName(property)));
                }
                return inner(context);
            };
        }
    }
}

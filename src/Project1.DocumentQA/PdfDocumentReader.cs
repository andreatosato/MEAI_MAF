using Microsoft.Extensions.DataIngestion;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Project1.DocumentQA;

/// <summary>
/// Reader personalizzato per file PDF usando PdfPig (100% .NET).
/// Estrae il testo da PDF e lo converte in IngestionDocument.
/// </summary>
public class PdfDocumentReader : IngestionDocumentReader
{
    public override Task<IngestionDocument> ReadAsync(
        FileInfo source,
        string identifier,
        string? mediaType = null,
        CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(source.FullName);

        var ingestionDoc = new IngestionDocument(identifier);

        foreach (Page page in document.GetPages())
        {
            var pageText = page.Text;

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                // Crea una sezione per ogni pagina
                var section = new IngestionDocumentSection();

                // Aggiungi un header per la pagina
                section.Elements.Add(new IngestionDocumentHeader($"# Pagina {page.Number}"));

                // Aggiungi il contenuto come paragrafo
                section.Elements.Add(new IngestionDocumentParagraph(pageText));

                ingestionDoc.Sections.Add(section);
            }
        }

        return Task.FromResult(ingestionDoc);
    }

    public override Task<IngestionDocument> ReadAsync(
        Stream source,
        string identifier,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(source);

        var ingestionDoc = new IngestionDocument(identifier);

        foreach (Page page in document.GetPages())
        {
            var pageText = page.Text;

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                // Crea una sezione per ogni pagina
                var section = new IngestionDocumentSection();

                // Aggiungi un header per la pagina
                section.Elements.Add(new IngestionDocumentHeader($"# Pagina {page.Number}"));

                // Aggiungi il contenuto come paragrafo
                section.Elements.Add(new IngestionDocumentParagraph(pageText));

                ingestionDoc.Sections.Add(section);
            }
        }

        return Task.FromResult(ingestionDoc);
    }
}

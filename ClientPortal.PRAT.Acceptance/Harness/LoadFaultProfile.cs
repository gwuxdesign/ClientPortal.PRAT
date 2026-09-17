using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Harness;

/// <summary>
/// Simulates environment load on the Documents page: intercepts the
/// document metadata response and appends 'magnitude' extra copies of
/// its 'documents' array (each clone given a fresh GUID so nothing
/// collides on documentGuid), testing whether filtering, sorting and
/// rendering hold up against a far larger dataset than REL's current
/// test data (6 documents).
///
/// Unlike the network-delay profiles, this targets one specific
/// endpoint and rewrites the response body rather than affecting
/// timing. Applied via the same MenuSteps.TheUserClicksThe call site
/// already wired for Timing/Latency/Concurrency — Documents.feature
/// reaches the page through the same step, so no new wiring was needed.
///
/// Only the 'documents' array is inflated, not 'documentsForApproval':
/// confirmed against a real response that the rendered/filterable list
/// (the "All" filter's count of 6) matches 'documents' exactly,
/// 'documentsForApproval' is a separate, unrelated queue.
///
/// Scope note: this tests client-side rendering under volume only. The
/// real backend/database never sees the larger dataset, since the
/// response is rewritten after the real (small) response is fetched.
/// A UI-driven upload path exists but has no PRAT automation yet
/// (deprioritised by the org restructure), which is why this narrower,
/// front-end-only scope was chosen over seeding real data — see
/// docs/harness-design.md.
/// </summary>
public class LoadFaultProfile : IFaultProfile
{
    public string Name => "Load";

    public async Task ApplyAsync(IPage page, int magnitude, CancellationToken cancellationToken = default)
    {
        if (magnitude <= 0)
        {
            return;
        }

        await page.RouteAsync("**/api/document/all/metadata", async route =>
        {
            var response = await route.FetchAsync();
            var body = await response.TextAsync();
            var root = JsonNode.Parse(body)!.AsObject();

            var documents = root["documents"]!.AsArray();
            var originals = documents.Select(node => node!.DeepClone()).ToList();

            for (var i = 0; i < magnitude; i++)
            {
                foreach (var original in originals)
                {
                    var clone = original!.DeepClone()!.AsObject();
                    clone["documentGuid"] = Guid.NewGuid().ToString();
                    documents.Add(clone);
                }
            }

            // Strip content-length: it reflects the original, much
            // smaller body, and would mismatch the inflated one.
            var headers = response.Headers
                .Where(h => !string.Equals(h.Key, "content-length", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => h.Value);

            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = response.Status,
                Headers = headers,
                Body = root.ToJsonString()
            });
        });
    }
}
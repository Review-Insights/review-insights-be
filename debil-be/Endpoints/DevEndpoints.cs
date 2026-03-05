using debil_be.DTOs;
using debil_be.Entities;
using debil_be.Services;

namespace debil_be.Endpoints;

public static class DevEndpoints
{
    public static RouteGroupBuilder MapDevEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dev")
            .WithTags("Development");

        group.MapPost("/seed", async (IBlueprintService service, CancellationToken ct) =>
        {
            var request = new CreateBlueprintRequest
            {
                Name = "Customer Review Analysis",
                Description = "Analyze clothing reviews for root causes, issues, priority, churn risk, and draft responses",
                DataStructure = new Dictionary<string, string>
                {
                    ["Review Text"] = "Customer's detailed review of the clothing item",
                    ["Rating"] = "Star rating from 1-5",
                    ["Title"] = "Optional review headline",
                    ["Age"] = "Customer's age",
                    ["Recommended IND"] = "Whether customer recommends (0 or 1)",
                    ["Division Name"] = "Product division (General, Petite, etc.)",
                    ["Department Name"] = "Product department (Dresses, Tops, etc.)",
                    ["Class Name"] = "Specific product class"
                },
                Tasks =
                [
                    new CreateBlueprintTaskRequest
                    {
                        TaskType = "classification",
                        TaskName = "root_cause",
                        Description = "Identify the primary reason for customer satisfaction/dissatisfaction",
                        Question = "What is the main issue or highlight in this review?",
                        Values =
                        [
                            new TaskValue { Value = "sizing_issue", Examples = ["Too small, couldn't zip it up", "Runs large, had to size down", "Size chart is way off"] },
                            new TaskValue { Value = "quality_defect", Examples = ["Material feels cheap and thin", "Stitching came apart after one wear", "Zipper broke right away"] },
                            new TaskValue { Value = "fit_problem", Examples = ["Too tight in the bust area", "Waist doesn't fit right", "Unflattering on my body type"] },
                            new TaskValue { Value = "photo_mismatch", Examples = ["Color is completely different in person", "Not as pictured online", "Photos are misleading"] },
                            new TaskValue { Value = "style_expectation", Examples = ["Not my style after all", "Too casual for what I needed", "Looked better online"] },
                            new TaskValue { Value = "positive_experience", Examples = ["Perfect fit, love it!", "Exactly what I wanted", "Beautiful dress, great quality"] }
                        ]
                    },
                    new CreateBlueprintTaskRequest
                    {
                        TaskType = "extraction",
                        TaskName = "specific_issue",
                        Description = "Extract the specific product issue or praise mentioned",
                        Instruction = "Extract the specific issue or positive aspect mentioned in the review. Use the customer's own words when possible.",
                        Format = "short_phrase"
                    },
                    new CreateBlueprintTaskRequest
                    {
                        TaskType = "multi_select",
                        TaskName = "issues_present",
                        Description = "Identify all issues mentioned in the review",
                        Question = "Which issues are mentioned in this review? Select all that apply.",
                        Values =
                        [
                            new TaskValue { Value = "sizing", Examples = ["too small", "runs large", "size inconsistent"] },
                            new TaskValue { Value = "quality", Examples = ["cheap material", "poor stitching", "fabric pills"] },
                            new TaskValue { Value = "color", Examples = ["wrong color", "different from photo", "color faded"] },
                            new TaskValue { Value = "comfort", Examples = ["uncomfortable", "itchy", "too tight"] },
                            new TaskValue { Value = "style", Examples = ["not flattering", "unflattering cut", "ugly design"] },
                            new TaskValue { Value = "price", Examples = ["too expensive", "not worth the price", "overpriced"] },
                            new TaskValue { Value = "none", Examples = ["no issues", "perfect", "love it"] }
                        ]
                    },
                    new CreateBlueprintTaskRequest
                    {
                        TaskType = "classification",
                        TaskName = "priority",
                        Description = "Determine the urgency level for addressing this review",
                        Question = "How urgently does this review need attention?",
                        Values =
                        [
                            new TaskValue { Value = "urgent", Examples = ["Going to write bad reviews everywhere", "Demanding refund immediately"] },
                            new TaskValue { Value = "high", Examples = ["Very disappointed, won't buy again", "Multiple issues mentioned"] },
                            new TaskValue { Value = "medium", Examples = ["Mixed feelings about purchase", "Could be better"] },
                            new TaskValue { Value = "low", Examples = ["Mostly positive with minor note", "Great product, tiny issue"] }
                        ]
                    },
                    new CreateBlueprintTaskRequest
                    {
                        TaskType = "classification",
                        TaskName = "churn_risk",
                        Description = "Assess likelihood customer will stop purchasing from this brand",
                        Question = "What is the likelihood this customer will stop purchasing from this brand?",
                        Values =
                        [
                            new TaskValue { Value = "high_risk", Examples = ["Worst purchase ever, never again", "Done with this brand"] },
                            new TaskValue { Value = "medium_risk", Examples = ["Disappointed, might try again but unsure", "Reconsidering future purchases"] },
                            new TaskValue { Value = "low_risk", Examples = ["Usually love this brand, one bad item", "Still a loyal customer"] },
                            new TaskValue { Value = "no_risk", Examples = ["Love this brand, buying more!", "Absolutely recommend to everyone"] }
                        ]
                    },
                    new CreateBlueprintTaskRequest
                    {
                        TaskType = "generation",
                        TaskName = "response_draft",
                        Description = "Draft a customer service response addressing the review",
                        Instruction = "Write a professional, empathetic customer service response that addresses the customer's specific concerns.",
                        MaxLength = 100
                    },
                    new CreateBlueprintTaskRequest
                    {
                        TaskType = "boolean",
                        TaskName = "requires_immediate_action",
                        Description = "Determine if this review requires immediate escalation",
                        Question = "Does this review require immediate action from customer service management?"
                    }
                ]
            };

            var blueprint = await service.CreateAsync(request, ct);
            return Results.Created($"/api/blueprints/{blueprint.Id}", blueprint);
        }).WithName("SeedData");

        return group;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "DepartmentRequester,DepartmentRepresentative")]
[AutoValidateAntiforgeryToken]
public class DepartmentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public DepartmentController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private ApplicationUser? CurrentEmployee()
    {
        string? id = _users.GetUserId(User);
        return _db.Users.FirstOrDefault(u => u.Id == id && u.IsActive && u.DepartmentId != null && u.Department!.IsActive);
    }
    private IQueryable<ProcurementRequest> Mine(ApplicationUser user) =>
        _db.ProcurementRequests.Where(r => r.DepartmentId == user.DepartmentId);
    private void Log(string action, int id) => _db.AuditLogs.Add(new AuditLog
    {
        UserId = _users.GetUserId(User), Action = action, EntityName = nameof(ProcurementRequest), EntityId = id.ToString()
    });
    private static ProcurementRequest MakeForm(ProcurementRequest request) => new()
    {
        Id = request.Id, Title = request.Title, Description = request.Description,
        Priority = request.Priority, RequiredByDate = request.RequiredByDate,
        Items = request.Items.Select(i => new ProcurementRequestItem
        {
            CategoryId = i.CategoryId, ItemName = i.ItemName, Specifications = i.Specifications,
            Quantity = i.Quantity, Unit = i.Unit, EstimatedUnitPrice = i.EstimatedUnitPrice
        }).ToList()
    };
    private bool ValidateForm(ProcurementRequest form)
    {
        // Validate only the user-editable fields. The entity itself has required
        // server-owned fields (RequestNumber, Department, RequestedByUser).
        bool invalidInput = ModelState.Values.SelectMany(v => v.Errors)
            .Any(e => e.Exception != null);
        ModelState.Clear();
        if (invalidInput)
            ModelState.AddModelError("", "Some entered values are invalid.");
        if (string.IsNullOrWhiteSpace(form.Title) || form.Title.Trim().Length > 200)
            ModelState.AddModelError(nameof(form.Title), "Enter a title (up to 200 characters).");
        if (string.IsNullOrWhiteSpace(form.Description) || form.Description.Length > 2000)
            ModelState.AddModelError(nameof(form.Description), "Enter a description / reason (up to 2000 characters).");
        if (!Enum.IsDefined(form.Priority))
            ModelState.AddModelError(nameof(form.Priority), "Invalid priority.");
        if (!form.RequiredByDate.HasValue || form.RequiredByDate.Value.Date < DateTime.Today)
            ModelState.AddModelError(nameof(form.RequiredByDate), "Choose today or a future required date.");
        form.Items ??= new List<ProcurementRequestItem>();
        if (form.Items.Count > 30)
            ModelState.AddModelError("Items", "Maximum 30 items per request.");
        form.Items = form.Items.Where(i => !string.IsNullOrWhiteSpace(i.ItemName) ||
            !string.IsNullOrWhiteSpace(i.Specifications) || !string.IsNullOrWhiteSpace(i.Unit) ||
            i.Quantity != 0 || i.EstimatedUnitPrice != 0 || i.CategoryId != null).ToList();
        if (form.Items.Count == 0)
            ModelState.AddModelError("Items", "Add at least one item.");
        foreach (var item in form.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ItemName) || item.ItemName.Length > 250 ||
                string.IsNullOrWhiteSpace(item.Unit) || item.Unit.Length > 30 ||
                item.Specifications?.Length > 2000 || item.Quantity <= 0 || item.EstimatedUnitPrice < 0 ||
                item.Quantity > 1000000m || item.EstimatedUnitPrice > 1000000000m)
                ModelState.AddModelError("Items", "Check item name, unit, quantity and estimated price.");
            if (item.CategoryId.HasValue && !_db.Categories.Any(c => c.Id == item.CategoryId.Value))
                ModelState.AddModelError("Items", "Invalid item category.");
        }
        // 18,2 budget can be stored safely only up to 16 digits before the decimal.
        if (form.Items.Sum(i => i.Quantity * i.EstimatedUnitPrice) > 9999999999999999.99m)
            ModelState.AddModelError("Items", "Estimated budget exceeds the allowed range.");
        return ModelState.IsValid;
    }
    private static void SetItems(ProcurementRequest request, ProcurementRequest form)
    {
        foreach (var item in form.Items)
            request.Items.Add(new ProcurementRequestItem
            {
                CategoryId = item.CategoryId, ItemName = item.ItemName.Trim(),
                Specifications = item.Specifications?.Trim(), Quantity = item.Quantity,
                Unit = item.Unit.Trim(), EstimatedUnitPrice = item.EstimatedUnitPrice
            });
        request.EstimatedBudget = form.Items.Sum(i => i.Quantity * i.EstimatedUnitPrice);
    }

    public IActionResult Index(string? status, string? search)
    {
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        var requests = Mine(user);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProcurementRequestStatus>(status, true, out var state))
            requests = requests.Where(r => r.Status == state);
        if (!string.IsNullOrWhiteSpace(search))
            requests = requests.Where(r => r.Title.Contains(search) || r.RequestNumber.Contains(search));
        ViewBag.Status = status;
        ViewBag.Search = search;
        return View(requests.OrderByDescending(r => r.RequestedAt).ToList());
    }

    public IActionResult Dashboard()
    {
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        var requests = Mine(user);
        ViewBag.Total = requests.Count();
        ViewBag.Draft = requests.Count(r => r.Status == ProcurementRequestStatus.Draft);
        ViewBag.Submitted = requests.Count(r => r.Status == ProcurementRequestStatus.Submitted);
        ViewBag.UnderReview = requests.Count(r => r.Status == ProcurementRequestStatus.UnderReview);
        ViewBag.Returned = requests.Count(r => r.Status == ProcurementRequestStatus.Returned);
        ViewBag.Rejected = requests.Count(r => r.Status == ProcurementRequestStatus.Rejected);
        ViewBag.Approved = requests.Count(r => r.Status == ProcurementRequestStatus.Approved);
        ViewBag.Converted = requests.Count(r => r.Status == ProcurementRequestStatus.ConvertedToTender);
        ViewBag.Notifications = _db.Notifications.Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.CreatedAt).Take(10).ToList();
        return View(requests.OrderByDescending(r => r.RequestedAt).Take(10).ToList());
    }

    public IActionResult Details(int id)
    {
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        var request = Mine(user).Include(r => r.Items).Include(r => r.Reviews)
            .ThenInclude(r => r.ReviewerUser).Include(r => r.Tenders)
            .FirstOrDefault(r => r.Id == id);
        if (request == null) return NotFound();
        return View(request);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (CurrentEmployee() == null) return Forbid();
        return View(new ProcurementRequest { Items = new List<ProcurementRequestItem> { new() } });
    }

    [HttpPost]
    public IActionResult Create(string title, string? description, RequestPriority priority,
        DateTime? requiredByDate, List<ProcurementRequestItem> items)
    {
        var form = new ProcurementRequest
        {
            Title = title ?? "", Description = description, Priority = priority,
            RequiredByDate = requiredByDate,
            Items = items ?? new List<ProcurementRequestItem>()
        };
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        if (!ValidateForm(form)) return View(form);
        var request = new ProcurementRequest
        {
            RequestNumber = "PR-" + Guid.NewGuid().ToString("N")[..24].ToUpperInvariant(),
            Title = form.Title.Trim(), Description = form.Description?.Trim(),
            Priority = form.Priority, RequiredByDate = form.RequiredByDate,
            DepartmentId = user.DepartmentId!.Value, RequestedByUserId = user.Id,
            Status = ProcurementRequestStatus.Draft
        };
        SetItems(request, form);
        using var transaction = _db.Database.BeginTransaction();
        _db.ProcurementRequests.Add(request);
        _db.SaveChanges();
        Log("CreateDraft", request.Id);
        _db.SaveChanges();
        transaction.Commit();
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        var request = Mine(user).Include(r => r.Items).FirstOrDefault(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != ProcurementRequestStatus.Draft && request.Status != ProcurementRequestStatus.Returned)
            return BadRequest("Only Draft or Returned requests can be edited.");
        return View(MakeForm(request));
    }

    [HttpPost]
    public IActionResult Edit(int id, string title, string? description, RequestPriority priority,
        DateTime? requiredByDate, List<ProcurementRequestItem> items)
    {
        var form = new ProcurementRequest
        {
            Id = id, Title = title ?? "", Description = description, Priority = priority,
            RequiredByDate = requiredByDate,
            Items = items ?? new List<ProcurementRequestItem>()
        };
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        var request = Mine(user).Include(r => r.Items).FirstOrDefault(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != ProcurementRequestStatus.Draft && request.Status != ProcurementRequestStatus.Returned)
            return BadRequest("Only Draft or Returned requests can be edited.");
        if (form.Id != id) return BadRequest();
        if (!ValidateForm(form)) return View(form);
        request.Title = form.Title.Trim();
        request.Description = form.Description?.Trim();
        request.Priority = form.Priority;
        request.RequiredByDate = form.RequiredByDate;
        _db.ProcurementRequestItems.RemoveRange(request.Items);
        request.Items.Clear();
        SetItems(request, form);
        Log("EditDraft", id);
        _db.SaveChanges();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public IActionResult Submit(int id)
    {
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        var request = Mine(user).Include(r => r.Items).FirstOrDefault(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != ProcurementRequestStatus.Draft && request.Status != ProcurementRequestStatus.Returned)
            return BadRequest("Only Draft or Returned requests may be submitted.");
        if (!request.Items.Any() || request.RequiredByDate == null || request.RequiredByDate.Value.Date < DateTime.Today)
            return BadRequest("Provide at least one item and a valid required date before submitting.");
        request.Status = ProcurementRequestStatus.Submitted;
        Log("SubmitRequest", id);
        _db.SaveChanges();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public IActionResult Delete(int id)
    {
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        var request = Mine(user).FirstOrDefault(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != ProcurementRequestStatus.Draft) return BadRequest("Only Draft requests can be deleted.");
        return View(request);
    }

    [HttpPost, ActionName("Delete")]
    public IActionResult DeleteConfirmed(int id)
    {
        var user = CurrentEmployee();
        if (user == null) return Forbid();
        var request = Mine(user).Include(r => r.Items).FirstOrDefault(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != ProcurementRequestStatus.Draft) return BadRequest("Only Draft requests can be deleted.");
        if (_db.Clarifications.Any(c => c.ProcurementRequestId == id) ||
            _db.ProcessMessages.Any(m => m.ProcurementRequestId == id) ||
            _db.WorkAssignments.Any(a => a.ProcurementRequestId == id) ||
            _db.Tenders.Any(t => t.ProcurementRequestId == id) ||
            _db.ProcurementRequestReviews.Any(r => r.ProcurementRequestId == id) ||
            _db.WorkflowTransitions.Any(t => t.ProcurementRequestId == id))
            return BadRequest("This request has related records and cannot be deleted.");
        _db.ProcurementRequestItems.RemoveRange(request.Items);
        _db.ProcurementRequests.Remove(request);
        _db.SaveChanges();
        return RedirectToAction(nameof(Index));
    }
}

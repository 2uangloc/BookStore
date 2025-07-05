using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookStore.DataAccess.Data;
using BookStore.DataAccess.Repository.IRepository;
using BookStore.Models;
using BookStore.Utility;
using System.Security.Claims;
using BookStore.Services.Logging;
using Microsoft.AspNetCore.Identity;

namespace BookStoreWeb.Areas.Employee.Controllers
{
    [Area("Employee")]
    [Authorize(Roles = SD.Role_Employee)]
    public class CategoryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IAuditLogger _auditLogger;
        public CategoryController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment,
            UserManager<IdentityUser> userManager, ApplicationDbContext db, IAuditLogger auditLogger)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _db = db;
            _auditLogger = auditLogger;
        }
        public IActionResult Index()
        {
            List<Category> objCategory = _unitOfWork.Category.GetAll().ToList();

            return View(objCategory);
        }



        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Create(Category obj)
        {
            if (obj.Name == obj.DisplayOrder.ToString())
            {
                ModelState.AddModelError("name", "The Display Order can not mactch the Name");
            }
            //Kiểm tra dữ liệu hợp lệ hay không
            if (ModelState.IsValid)
            {
                _unitOfWork.Category.Add(obj);
                _unitOfWork.Save();
                TempData["success"] = "Category created successfully";
                //ghi log khi create
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserName = User.Identity?.Name;
                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditLogger.LogAsync(
                    userId: currentUserId,
                    userName: currentUserName,
                    action: "Create",
                    entityName: "Category",
                    entityId: obj.Id.ToString(),
                    ipAddress: ipAddress,
                    description: $"Create category: {obj.Name}"
                );
                //chuyển hướng người dùng về trang "Index"
                return RedirectToAction("Index");
            }
            //Nếu dữ liệu không hợp lệ, Trả về lại trang Create, giữ nguyên dữ liệu để người dùng sửa lỗi
            return View();
        }




        public IActionResult Edit(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            Category? categoryFromDb = _unitOfWork.Category.GetValue(u => u.Id == id);
            if (categoryFromDb == null)
            {
                return NotFound();
            }
            return View(categoryFromDb);
        }
        [HttpPost]
        public async Task<IActionResult> Edit(Category obj)
        {
            if (obj.Name == obj.DisplayOrder.ToString())
            {
                ModelState.AddModelError("name", "The Display Order can not mactch the Name");
            }
            //Kiểm tra dữ liệu hợp lệ hay không
            if (ModelState.IsValid)
            {
                _unitOfWork.Category.Update(obj);
                _unitOfWork.Save();
                TempData["Success"] = "Category updated successfully";
                //ghi log khi Update
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserName = User.Identity?.Name;
                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditLogger.LogAsync(
                    userId: currentUserId,
                    userName: currentUserName,
                    action: "Update",
                    entityName: "Category",
                    entityId: obj.Id.ToString(),
                    ipAddress: ipAddress,
                    description: $"Update category: {obj.Name}"
                );
                //chuyển hướng người dùng về trang "Index"
                return RedirectToAction("Index");
            }
            //Nếu dữ liệu không hợp lệ, Trả về lại trang Create, giữ nguyên dữ liệu để người dùng sửa lỗi
            return View();
        }




        public IActionResult Delete(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            Category categoryFromDb = _unitOfWork.Category.GetValue(u => u.Id == id);
            if (categoryFromDb == null)
            {
                return NotFound();
            }
            return View(categoryFromDb);
        }
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeletePost(int? id)
        {
            Category? obj = _unitOfWork.Category.GetValue(u => u.Id == id);
            if (obj == null)
            {
                return NotFound();
            }
            _unitOfWork.Category.Remove(obj);
            _unitOfWork.Save();
            TempData["Success"] = "Category deleted successfully";
            //ghi log khi Update
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserName = User.Identity?.Name;
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditLogger.LogAsync(
                userId: currentUserId,
                userName: currentUserName,
                action: "Delete",
                entityName: "Category",
                entityId: obj.Id.ToString(),
                ipAddress: ipAddress,
                description: $"Delete category: {obj.Name}"
            );
            return RedirectToAction("Index");
        }
    }
}

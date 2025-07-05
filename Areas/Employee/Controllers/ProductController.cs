using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BookStore.DataAccess.Data;
using BookStore.DataAccess.Repository.IRepository;
using BookStore.Models;
using BookStore.Models.ViewModels;
using BookStore.Utility;
using System.Security.Claims;
using BookStore.Services.Logging;
using Microsoft.AspNetCore.Identity;
using System;

namespace BookStoreWeb.Areas.Employee.Controllers
{
    [Area("Employee")]
    [Authorize(Roles = SD.Role_Employee)]
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IAuditLogger _auditLogger;
        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment,
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
            List<Product> objProduct = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
            return View(objProduct);
        }
        public IActionResult Upsert(int? id) //Up = update, sert = insert
        {
            ProductVM productVM = new()
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                Product = new Product()

            };
            if (id == null || id == 0)
            {
                //create
                return View(productVM);
            }
            else
            {
                //update
                productVM.Product = _unitOfWork.Product.GetValue(u => u.Id == id);
                return View(productVM);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Upsert(ProductVM productVM, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;

                if (file != null)
                {
                    // ✅ Đường dẫn thư mục ảnh
                    string productPath = Path.Combine(wwwRootPath, "images", "Product");

                    // ✅ Tạo tên file mới
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);

                    // ✅ Xóa ảnh cũ nếu có (khi update)
                    if (!string.IsNullOrEmpty(productVM.Product.ImageUrl))
                    {
                        string oldImagePath = Path.Combine(wwwRootPath, productVM.Product.ImageUrl.TrimStart('\\', '/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    // ✅ Ghi ảnh mới
                    using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    // ✅ Gán lại đường dẫn ảnh mới (dùng dấu `/` cho đường dẫn web)
                    //productVM.Product.ImageUrl = Path.Combine("images", "Product", fileName).Replace("\\", "/");
                    productVM.Product.ImageUrl = $"/images/Product/{fileName}";

                }
                string action;
                if (productVM.Product.Id == 0)
                {
                    _unitOfWork.Product.Add(productVM.Product);
                    action = "Create";
                    TempData["success"] = "Product created successfully";
                }
                else
                {
                    _unitOfWork.Product.Update(productVM.Product);
                    action = "Update";
                    TempData["success"] = "Product updated successfully";
                }
                _unitOfWork.Save();
                //ghi log khi create hoac update
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserName = User.Identity?.Name;
                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditLogger.LogAsync(
                    userId: currentUserId,
                    userName: currentUserName,
                    action: action,
                    entityName: "Product",
                    entityId: productVM.Product.Id.ToString(),
                    ipAddress: ipAddress,
                    description: $"{action} product: {productVM.Product.Title}"
                );
                return RedirectToAction("Index");
            }

            // Nếu ModelState không hợp lệ
            productVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
            {
                Text = u.Name,
                Value = u.Id.ToString()
            });

            return View(productVM);
        }

        #region API CALLS
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Product> objProduct = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
            return Json(new { data = objProduct });
        }
        [HttpDelete]
        public async Task<IActionResult> Delete(int? id)
        {
            var productToBeDeleted = _unitOfWork.Product.GetValue(u => u.Id == id);
            if (productToBeDeleted == null)
            {
                return Json(new { success = false, message = "Error While Deleting" });
            }

            if (!string.IsNullOrEmpty(productToBeDeleted.ImageUrl))
            {
                var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, productToBeDeleted.ImageUrl.TrimStart('\\'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }
            }

            _unitOfWork.Product.Remove(productToBeDeleted);
            _unitOfWork.Save();

            // ✅ Ghi log xóa sản phẩm
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserName = User.Identity?.Name;
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _auditLogger.LogAsync(
                userId: currentUserId,
                userName: currentUserName,
                action: "Delete",
                entityName: "Product",
                entityId: productToBeDeleted.Id.ToString(),
                ipAddress: ipAddress,
                description: $"Deleted product: {productToBeDeleted.Title}"
            );

            return Json(new { success = true, message = "Delete Successful" });
        }

        #endregion
    }
}

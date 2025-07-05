using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BookStore.DataAccess.Data;
using BookStore.DataAccess.Repository.IRepository;
using BookStore.Models;
using BookStore.Models.ViewModels;
using BookStore.Utility;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using BookStore.Services.Logging;
using System.Net;
using Stripe;

namespace BookStoreWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class UserController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IAuditLogger _auditLogger;
        public UserController(IUnitOfWork unitOfWork, UserManager<IdentityUser> userManager,
            ApplicationDbContext db, IAuditLogger auditLogger)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _db = db;
            _auditLogger = auditLogger;
        }
        public IActionResult Index()
        {

            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = _unitOfWork.ApplicationUser.GetValue(u => u.Id == id);
            if (user == null)
                return NotFound();

            // Lấy roles của user
            var userRoles = await _userManager.GetRolesAsync(user);
            bool isCustomer = userRoles.Contains(SD.Role_Customer);
            // Khởi tạo ViewModel
            var viewModel = new UserDetailVM
            {
                User = user,
                Orders = isCustomer
            ? _unitOfWork.OrderHeader.GetAll(o => o.ApplicationUserId == id).ToList()
            : new List<OrderHeader>(),
                IsCustomer = isCustomer
            };

            // Nếu user là Customer thì lấy đơn hàng
            if (userRoles.Contains(SD.Role_Customer))
            {
                viewModel.Orders = _unitOfWork.OrderHeader
                    .GetAll(o => o.ApplicationUserId == id)
                    .ToList();
            }

            return View(viewModel);
        }
        //thay doi role
        //[HttpPost]
        //public async Task<IActionResult> ChangeRole(string userId, string newRole)
        //{
        //    var user = await _userManager.FindByIdAsync(userId);
        //    var currentRoles = await _userManager.GetRolesAsync(user);

        //    await _userManager.RemoveFromRolesAsync(user, currentRoles);
        //    await _userManager.AddToRoleAsync(user, newRole);

        //    return RedirectToAction(nameof(Details), new { userId });
        //}


        //xoa user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            // Không cho phép xóa tài khoản admin gốc
            if (user.Email == SD.SuperAdminEmail)
            {
                TempData["Error"] = "Không thể xóa tài khoản admin gốc.";
                return RedirectToAction(nameof(Index));
            }

            // Xóa user
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                // Ghi log 
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserName = User.Identity?.Name;
                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditLogger.LogAsync(
                    userId: currentUserId,
                    userName: currentUserName,
                    action: "Delete",
                    entityName: "User",
                    entityId: user.Id,
                    ipAddress: ipAddress,
                    description: $"Đã xóa tài khoản: {user.Email}"
                );

                TempData["Success"] = "Xóa người dùng thành công!";
            }
            else
            {
                TempData["Error"] = "Xóa người dùng thất bại!";
            }

            return RedirectToAction(nameof(Index));
        }





        [HttpGet]
        public async Task<IActionResult> Permission(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var user = _unitOfWork.ApplicationUser.GetValue(u => u.Id == userId);

            if (user == null)
                return NotFound();

            // Nếu user là admin gốc thì không cho phép đổi
            if (user.Email == SD.SuperAdminEmail)
            {
                TempData["Error"] = "Không thể thay đổi vai trò của admin gốc.";
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var model = new ChangeRoleVM
            {
                UserId = userId,
                CurrentRole = currentRoles.FirstOrDefault(),
                AvailableRoles = new List<SelectListItem>
        {
            new SelectListItem{ Text = SD.Role_Admin, Value = SD.Role_Admin },
            new SelectListItem{ Text = SD.Role_Employee, Value = SD.Role_Employee },
            new SelectListItem{ Text = SD.Role_Customer, Value = SD.Role_Customer },
        }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Permission(ChangeRoleVM model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return NotFound();

            // Không cho phép thay đổi vai trò của admin gốc
            if (user.Email == SD.SuperAdminEmail)
            {
                TempData["Error"] = "Không thể thay đổi vai trò của admin gốc.";
                return RedirectToAction(nameof(Details), new { id = model.UserId });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                TempData["Error"] = "Xoá vai trò cũ thất bại.";
                return RedirectToAction(nameof(Details), new { id = model.UserId });
            }

            var addResult = await _userManager.AddToRoleAsync(user, model.NewRole);
            if (!addResult.Succeeded)
            {
                TempData["Error"] = "Thêm vai trò mới thất bại.";
                return RedirectToAction(nameof(Details), new { id = model.UserId });
            }

            // ✅ Ghi log thay đổi
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserName = User.Identity.Name;
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditLogger.LogAsync(
                userId: currentUserId,
                userName: currentUserName,
                action: "Thay đổi vai trò",
                entityName: "User",
                entityId: user.Id,
                ipAddress: ipAddress,
                description: $"{string.Join(",", currentRoles)} => {model.NewRole}"
            );

            TempData["Success"] = "Cập nhật vai trò thành công!";
            return RedirectToAction(nameof(Details), new { id = model.UserId });
        }










        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string role)
        {
            IEnumerable<ApplicationUser> objUserList = _db.ApplicationUsers.ToList();
            var userRoles = _db.UserRoles.ToList();
            var roles = _db.Roles.ToList();
            foreach (var user in objUserList)
            {
                var roleId = userRoles.FirstOrDefault(u => u.UserId == user.Id).RoleId;
                user.Role = roles.FirstOrDefault(u => u.Id == roleId).Name;

            }
            
            switch (role?.ToLower())
            {
                case "admin":
                    objUserList = objUserList.Where(u => u.Role == SD.Role_Admin);
                    break;

                case "employee":
                    objUserList = objUserList.Where(u => u.Role == SD.Role_Employee);
                    break;

                case "customer":
                    objUserList = objUserList.Where(u => u.Role == SD.Role_Customer);
                    break;

                default:
                    // Lấy tất cả
                    break;
            }
            // Sau khi đã lọc objOrderHeader theo status...

            var result = objUserList.Select(o => new
            {
                o.Id,
                o.Name,
                o.PhoneNumber,
                o.Email,
                 o.Role
            });
            return Json(new { data = objUserList });
        }

        [HttpPost]
        public async Task<IActionResult> LockAndUnLock([FromBody] string id)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == id);
            if (objFromDb == null)
                return Json(new { success = false, message = "Không tìm thấy người dùng." });

            string action = "";

            if (objFromDb.LockoutEnd != null && objFromDb.LockoutEnd > DateTime.Now)
            {
                // Đang bị khóa → Mở khóa
                objFromDb.LockoutEnd = DateTime.Now;
                action = "Unlock User";
            }
            else
            {
                // Chưa bị khóa → Khóa
                objFromDb.LockoutEnd = DateTime.Now.AddYears(100);
                action = "Lock User";
            }

            _db.SaveChanges();

            // Ghi log hành động
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserName = User.Identity?.Name ?? "Unknown";
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _auditLogger.LogAsync(
                userId: currentUserId,
                userName: currentUserName,
                action: action,
                entityName: "User",
                entityId: objFromDb.Id,
                ipAddress: ipAddress,
                description: $"{action} '{objFromDb.Email}'"
            );

            return Json(new { success = true, message = "Thao tác thành công." });
        }




        #endregion
    }
}

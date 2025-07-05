using BookStore.DataAccess.Data;
using BookStore.DataAccess.Repository.IRepository;
using BookStore.Models;
using BookStore.Services.Logging;
using BookStore.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStoreWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class AuditLogController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IAuditLogger _auditLogger;
        public AuditLogController(IUnitOfWork unitOfWork, UserManager<IdentityUser> userManager,
            ApplicationDbContext db, IAuditLogger auditLogger)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _db = db;
            _auditLogger = auditLogger;
        }

        public IActionResult Index()
        {
            var logs = _unitOfWork.AuditLogs.GetAll()
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToList();

            return View(logs);
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

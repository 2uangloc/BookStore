using BookStore.DataAccess.Repository.IRepository;
using BookStore.Models;
using BookStore.Models.ViewModels;
using BookStore.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Diagnostics;
using System.Security.Claims;
using Stripe.Climate;
using BookStore.Services.IService;

namespace BookStoreWeb.Areas.Employee.Controllers
{
    [Area("Employee")]
    [Authorize(Roles = SD.Role_Employee)]
    public class OrderEmployeeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderStatusService _orderService;
        [BindProperty]
        public OrderVM OrderVM { get; set; }
        public OrderEmployeeController(IUnitOfWork unitOfWork, IOrderStatusService orderService)
        {
            _unitOfWork = unitOfWork;
            _orderService = orderService;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Details(int orderId)
        {
            OrderVM = new OrderVM()
            {
                OrderHeader = _unitOfWork.OrderHeader.GetValue(u => u.Id == orderId, includeProperties: "ApplicationUser"),
                OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, includeProperties: "Product")
            };
            return View(OrderVM);
        }
        [HttpPost]
        public IActionResult UpdateOrderDetail(int orderId)
        {
            var orderHeaderFromDb = _unitOfWork.OrderHeader.GetValue(u => u.Id == OrderVM.OrderHeader.Id);

            orderHeaderFromDb.Name = OrderVM.OrderHeader.Name;
            orderHeaderFromDb.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
            orderHeaderFromDb.StreetAddress = OrderVM.OrderHeader.StreetAddress;
            orderHeaderFromDb.City = OrderVM.OrderHeader.City;
            orderHeaderFromDb.State = OrderVM.OrderHeader.State;
            orderHeaderFromDb.PostalCode = OrderVM.OrderHeader.PostalCode;
            if (!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
                orderHeaderFromDb.Carrier = OrderVM.OrderHeader.Carrier;
            if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
                orderHeaderFromDb.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
            _unitOfWork.OrderHeader.Update(orderHeaderFromDb);
            _unitOfWork.Save();
            TempData["Success"] = "Order Details Updated Successfully";
            return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });
        }
        [HttpPost]
        public IActionResult StartProcessing()
        {
            _orderService.StartProcessing(OrderVM.OrderHeader.Id);
            TempData["Success"] = "Order processing started successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }

        [HttpPost]
        public IActionResult ShipOrder()
        {
            _orderService.ShipOrder(
                OrderVM.OrderHeader.Id,
                OrderVM.OrderHeader.TrackingNumber,
                OrderVM.OrderHeader.Carrier
            );
            TempData["Success"] = "Order shipped successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder()
        {
            var result = await _orderService.CancelOrderAsync(OrderVM.OrderHeader.Id);
            if (!result)
            {
                TempData["Error"] = "Refund failed. Please try again.";
            }
            else
            {
                TempData["Success"] = "Order cancelled successfully";
            }
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }
        [HttpPost]
        public IActionResult MarkAsCompleted()
        {
            var orderHeader = _unitOfWork.OrderHeader.GetValue(u => u.Id == OrderVM.OrderHeader.Id);
            if (orderHeader.OrderStatus == SD.StatusShipped)
            {
                orderHeader.OrderStatus = SD.StatusCompleted;
                _unitOfWork.OrderHeader.Update(orderHeader);
                _unitOfWork.Save();

                TempData["Success"] = "Order marked as completed successfully.";
            }
            else
            {
                TempData["Error"] = "Only shipped orders can be marked as completed.";
            }

            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }



        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<OrderHeader> objOrderHeader;

            if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
            {
                objOrderHeader = _unitOfWork.OrderHeader.GetAll(
                    includeProperties: "ApplicationUser").ToList();
            }
            else
            {
                var claimsIdentity = (ClaimsIdentity?)User.Identity;
                var claim = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier);

                if (claim == null)
                {
                    // Người dùng chưa đăng nhập hoặc lỗi xác thực
                    return Json(new { data = new List<OrderHeader>() });
                }

                var userId = claim.Value;

                objOrderHeader = _unitOfWork.OrderHeader.GetAll(
                    u => u.ApplicationUserId == userId,
                    includeProperties: "ApplicationUser");
            }

            switch (status?.ToLower())
            {
                case "pending":
                    objOrderHeader = objOrderHeader.Where(u => u.OrderStatus == SD.StatusPending);
                    break;

                case "approved":
                    objOrderHeader = objOrderHeader.Where(u => u.OrderStatus == SD.StatusApproved);
                    break;

                case "inprocess":
                    objOrderHeader = objOrderHeader.Where(u => u.OrderStatus == SD.StatusInProcess);
                    break;

                case "shipped":
                    objOrderHeader = objOrderHeader.Where(u => u.OrderStatus == SD.StatusShipped);
                    break;

                case "completed":
                    objOrderHeader = objOrderHeader.Where(u => u.OrderStatus == SD.StatusCompleted);
                    break;

                case "cancelled":
                    objOrderHeader = objOrderHeader.Where(u => u.OrderStatus == SD.StatusCancelled);
                    break;

                case "refunded":
                    objOrderHeader = objOrderHeader.Where(u => u.OrderStatus == SD.StatusRefunded);
                    break;

                default:
                    // Lấy tất cả
                    break;
            }
            // Sau khi đã lọc objOrderHeader theo status...

            var result = objOrderHeader.Select(o => new
            {
                o.Id,
                o.Name,
                o.PhoneNumber,
                o.OrderTotal,
                o.OrderStatus,
                o.PaymentStatus,
                PaymentMethod = o.PaymentMethod.ToString(), // 👈 chuyển enum thành chuỗi
                ApplicationUser = new
                {
                    o.ApplicationUser.Email
                }
            });

            return Json(new { data = result });


        }

        #endregion
    }
}

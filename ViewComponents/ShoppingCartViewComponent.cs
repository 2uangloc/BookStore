using BookStore.DataAccess.Repository.IRepository;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStoreWeb.ViewComponents
{
    public class ShoppingCartViewComponent : ViewComponent
    {
        private readonly IUnitOfWork _unitOfWork;

        public ShoppingCartViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IViewComponentResult Invoke()
        {
            int count = 0;

            if (User.Identity.IsAuthenticated)
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var claim = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier);

                if (claim != null)
                {
                    count = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value).Count();
                }
            }

            return View(count);
        }
    }
}

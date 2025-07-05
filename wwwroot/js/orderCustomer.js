var dataTable;
$(document).ready(function () {
    var url = window.location.search;
    var status = "all";
    if (url.includes("pending")) status = "pending";
    else if (url.includes("approved")) status = "approved";
    else if (url.includes("inprocess")) status = "inprocess";
    else if (url.includes("shipped")) status = "shipped";
    else if (url.includes("completed")) status = "completed";
    else if (url.includes("cancelled")) status = "cancelled";
    else if (url.includes("refunded")) status = "refunded";

    loadDataTable(status);
});

function loadDataTable(status) {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: "/Customer/OrderCustomer/GetAll?status=" + status },
        "columns": [
            { data: "orderCode" },
            { data: "name" },
            { data: "phoneNumber" },
            { data: "email" },
            { data: "orderStatus" },
            { data: "paymentMethod" },
            { data: "orderTotal", render: $.fn.dataTable.render.number(',', '.', 2, '$') },
            {
                data: { id: 'id', lockoutEnd: "lockoutEnd" },
                render: function (data) {
                    return `<a href="/Customer/OrderCustomer/Details?orderId=${data.id}" class="btn btn-sm btn-primary">Details</a>
                    `;
                }
            }
        ]
    });
}

var dataTable;
$(document).ready(function () {
    var url = window.location.search.toLowerCase();

    let status = "all";
    const statuses = ["inprocess", "completed", "pending", "approved", "cancelled", "refunded", "shipped"];

    for (const s of statuses) {
        if (url.includes(s)) {
            status = s;
            break;
        }
    }

    loadDataTable(status);
});


function loadDataTable(status) {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: '/Employee/OrderEmployee/getall?status=' + status },
        "columns": [
            { data: 'id', "width": "5%" },
            { data: 'name', "width": "25%" },
            { data: 'phoneNumber', "width": "15%" },
            { data: 'applicationUser.email', "width": "15%" },
            { data: 'orderStatus', "width": "15%" },
            { data: 'paymentMethod', "width": "15%" },
            { data: 'orderTotal', "width": "15%" },
            {
                data: 'id',
                "render": function (data) {
                    return `
                        <div class="w-75 btn-group" role="group">
                            <a href="/Employee/OrderEmployee/details?orderId=${data}" class="btn btn-primary mx-2">
                                <i class="bi bi-pencil-square"></i> Detail
                            </a>
                        </div>`;
                },
                "width": "15%"
            }
        ]
    });
}

var dataTable;
$(document).ready(function () {
    var url = window.location.search.toLowerCase();

    let role = "all";
    const roles = ["customer", "admin", "employee"];

    for (const s of roles) {
        if (url.includes(s)) {
            role = s;
            break;
        }
    }

    loadDataTable(role);
});


function loadDataTable(role) {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: '/admin/user/getall?role=' + role },
        "columns": [
            { data: 'id', "width": "5%" },
            { data: 'name', "width": "25%" },
            { data: 'phoneNumber', "width": "15%" },
            { data: 'email', "width": "15%" },
            { data: 'role', "width": "15%" },
            {
                data: { id:'id', lockoutEnd:"lockoutEnd"},
                "render": function (data) {
                    var today = new Date().getTime();
                    var lockout = new Date(data.lockoutEnd).getTime();
                    if (lockout > today) {
                        return `
                        <div class="text-center"">
                            <a onclick=LockAndUnLock('${data.id}') class="btn btn-danger text-white" style="cursor:pointer; width:100px;">
                                <i class="bi bi-lock-fill"></i> Lock
                            </a>
                            
                            <a href="/admin/user/details?id=${data.id}" class="btn btn-danger text-white" style="cursor:pointer; width:100px;">
                                <i class="bi bi-pencil-square"></i> Details
                            </a>
                        </div>`;
                    }
                    else {
                        return `
                        <div class="text-center"">
                            <a onclick=LockAndUnLock('${data.id}') class="btn btn-success text-white" style="cursor:pointer; width:100px;">
                                <i class="bi bi-unlock-fill"></i> UnLock
                            </a>
                            <a href="/admin/user/details?id=${data.id}" class="btn btn-danger text-white" style="cursor:pointer; width:100px;">
                                <i class="bi bi-pencil-square"></i> Details
                            </a>
                        </div>`;
                    }
                },
                "width": "15%"
            }
        ]
    });
}

function LockAndUnLock(id) {
    $.ajax({
        type: "POST",
        url: '/Admin/User/LockAndUnLock',
        data: JSON.stringify(id),
        contentType: "application/json",
        success: function (data) {
            if (data.success) {
                toastr.success(data.message);
                dataTable.ajax.reload();
            }
        }
    })
}

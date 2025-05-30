backupApp.controller('HomeController', function ($scope, $location, ServerStatus, BackupList, AppService, AppUtils, DialogService, gettextCatalog) {
    $scope.backups = BackupList.watch($scope);

    $scope.folderContext = null;

    $scope.orderByFromState = function()
    {
        return ServerStatus.state.orderBy;
    }

    $scope.orderBy = null;

    $scope.openFolderContext = function(path) {
        BackupList.getFolderContext(path).then(function(context) {
            $scope.folderContext = context;
        });
    };

    $scope.doRun = function(id) {
        AppService.post('/backup/' + id + '/run').then(function() {
            if (ServerStatus.state.programState == 'Paused') {
                DialogService.dialog(gettextCatalog.getString('Server paused'), gettextCatalog.getString('Server is currently paused, do you want to resume now?'), [gettextCatalog.getString('No'), gettextCatalog.getString('Yes')], function(ix) {
                    if (ix == 1)
                        ServerStatus.resume();
                });

            }
        }, function() {});
    };

    $scope.doRestore = function(id) {
        $location.path('/restore/' + id);
    };

    $scope.doEdit = function(id) {
        $location.path('/edit/' + id);
    };

    $scope.doExport = function(id) {
        $location.path('/export/' + id);
    };

    $scope.doCompact = function(id) {
        AppService.post('/backup/' + id + '/compact');
    };

    $scope.doDelete = function(id, name) {
        $location.path('/delete/' + id);
    };

    $scope.doLocalDb = function(id) {
        $location.path('/localdb/' + id);
    };

    $scope.doRepairLocalDb = function(id, name) {
        AppService.post('/backup/' + id + '/repair');
    };

    $scope.doVerifyRemote = function(id, name) {
        AppService.post('/backup/' + id + '/verify');
    };

    $scope.doShowLog = function(id, name) {
        $location.path('/log/' + id);
    };

    $scope.doCommandLine = function(id, name) {
        $location.path('/commandline/' + id);
    };

    $scope.doCreateBugReport = function(id, name) {
        AppService.post('/backup/' + id + '/createreport');
    };
    
    $scope.doApplyOrder = function (orderby)
    {
        ServerStatus.state.orderBy = orderby;
        AppService.patch('/serversettings', { 'backup-list-sort-order': orderby });
        BackupList.reload();
    };

    $scope.$on('sortorder_changed', function() {
        $scope.orderBy = $scope.orderByFromState();
    });

    $scope.formatDuration = AppUtils.formatDuration;
});

﻿import viewModelBase = require("viewmodels/viewModelBase");
import app = require("durandal/app");

class createSampleData extends viewModelBase{

    isBusy = ko.observable(false);
    isEnable = ko.observable(true);
    isVisible =  ko.observable(false);
    classData = ko.observable<string>();

    generateSampleData() {
        this.isBusy(true);
        
        require(["commands/database/studio/createSampleDataCommand"], createSampleDataCommand => {
            new createSampleDataCommand(this.activeDatabase())
                .execute()
                .always(() => this.isBusy(false));
        });
    }

    activate(args) {
        super.activate(args);
        this.updateHelpLink('OGRN53');
    }

    showSampleDataClass() {
        require(["commands/database/studio/createSampleDataClassCommand"], createSampleDataClassCommand => {
            new createSampleDataClassCommand(this.activeDatabase())
                .execute()
                .done((results: string) => {
                    this.isVisible(true);
                    var data = results.replace("\r\n", "");

                    require(["viewmodels/common/showDataDialog"], showDataDialog => {
                        app.showDialog(new showDataDialog("Sample Data Classes", data));
                    });
                })
                .always(() => this.isBusy(false));
        });
    }
}

export = createSampleData; 
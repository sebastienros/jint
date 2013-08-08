/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-56.js
 * @description Object.defineProperty - 'name' property doesn't exist in 'O', test [[Configurable]] of 'name' property is set as false if it is absent in accessor descriptor 'desc' (8.12.9 step 4.b.i)
 */


function testcase() {
        var obj = {};
        var setFunc = function (value) {
            obj.setVerifyHelpProp = value;
        };
        var getFunc = function () {
            return 10;
        };

        Object.defineProperty(obj, "property", {
            set: setFunc,
            get: getFunc,
            enumerable: true
        });
        return accessorPropertyAttributesAreCorrect(obj, "property", getFunc, setFunc, "setVerifyHelpProp", true, false);
    }
runTestCase(testcase);

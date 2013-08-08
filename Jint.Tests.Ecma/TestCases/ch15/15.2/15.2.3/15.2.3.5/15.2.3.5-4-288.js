/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-288.js
 * @description Object.create - one property in 'Properties' is an Error object that uses Object's [[Get]] method to access the 'set' property (8.10.5 step 8.a)
 */


function testcase() {
        var errObj = new Error("error");
        var data = "data";

        errObj.set = function (value) {
            data = value;
        };

        var newObj = Object.create({}, {
            prop: errObj
        });

        newObj.prop = "overrideData";

        return newObj.hasOwnProperty("prop") && data === "overrideData";
    }
runTestCase(testcase);

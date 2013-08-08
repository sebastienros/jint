/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-285.js
 * @description Object.create - one property in 'Properties' is a Date object that uses Object's [[Get]] method to access the 'set' property (8.10.5 step 8.a)
 */


function testcase() {
        var dateObj = new Date();
        var data = "data";
        dateObj.set = function (value) {
            data = value;
        };

        var newObj = Object.create({}, {
            prop: dateObj
        });

        var hasProperty = newObj.hasOwnProperty("prop");

        newObj.prop = "overrideData";

        return hasProperty && data === "overrideData";
    }
runTestCase(testcase);

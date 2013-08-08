/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-249.js
 * @description Object.create - one property in 'Properties' is a Date object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var dateObj = new Date();

        dateObj.get = function () {
            return "VerifyDateObject";
        };

        var newObj = Object.create({}, {
            prop: dateObj 
        });

        return newObj.prop === "VerifyDateObject";
    }
runTestCase(testcase);

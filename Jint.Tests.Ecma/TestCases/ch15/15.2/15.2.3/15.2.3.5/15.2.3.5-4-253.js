/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-253.js
 * @description Object.create - one property in 'Properties' is an Error object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var errObj = new Error("error");

        errObj.get = function () {
            return "VerifyErrorObject";
        };

        var newObj = Object.create({}, {
            prop: errObj 
        });

        return newObj.prop === "VerifyErrorObject";
    }
runTestCase(testcase);

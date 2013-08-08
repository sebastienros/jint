/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-244.js
 * @description Object.create - one property in 'Properties' is a Function object which implements its own [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var funObj = function () {};

        funObj.get = function () {
            return "VerifyFunctionObject";
        };

        var newObj = Object.create({}, {
            prop: funObj
        });

        return newObj.prop === "VerifyFunctionObject";
    }
runTestCase(testcase);

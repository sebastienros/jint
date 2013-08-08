/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-247.js
 * @description Object.create - one property in 'Properties' is a Boolean object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var boolObj = new Boolean(true);

        boolObj.get = function () {
            return "VerifyBooleanObject";
        };

        var newObj = Object.create({}, {
            prop: boolObj 
        });

        return newObj.prop === "VerifyBooleanObject";
    }
runTestCase(testcase);

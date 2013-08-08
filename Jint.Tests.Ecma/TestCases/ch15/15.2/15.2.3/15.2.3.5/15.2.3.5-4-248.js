/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-248.js
 * @description Object.create - one property in 'Properties' is a Number object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var numObj = new Number(5);

        numObj.get = function () {
            return "VerifyNumberObject";
        };

        var newObj = Object.create({}, {
            prop: numObj 
        });

        return newObj.prop === "VerifyNumberObject";
    }
runTestCase(testcase);

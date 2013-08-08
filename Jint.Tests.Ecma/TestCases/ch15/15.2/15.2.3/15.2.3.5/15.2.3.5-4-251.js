/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-251.js
 * @description Object.create - one property in 'Properties' is the Math object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        try {
            Math.get = function () {
                return "VerifyMathObject";
            };

            var newObj = Object.create({}, {
                prop: Math 
            });

            return newObj.prop === "VerifyMathObject";
        } finally {
            delete Math.get;
        }
    }
runTestCase(testcase);

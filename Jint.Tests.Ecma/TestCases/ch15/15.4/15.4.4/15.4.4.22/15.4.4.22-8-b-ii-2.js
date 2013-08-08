/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-ii-2.js
 * @description Array.prototype.reduceRight - deleted properties in step 2 are visible here
 */


function testcase() {

        var obj = { 2: "accumulator", 3: "another" };

        Object.defineProperty(obj, "length", {
            get: function () {
                delete obj[2];
                return 5;
            },
            configurable: true
        });

        return "accumulator" !== Array.prototype.reduceRight.call(obj, function () { });
    }
runTestCase(testcase);

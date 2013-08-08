/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-146-1.js
 * @description Object.defineProperty - 'Attributes' is a RegExp object that uses Object's [[Get]] method to access the 'value' property of prototype object  (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};
        try {
            RegExp.prototype.value = "RegExp";
            var regObj = new RegExp();

            Object.defineProperty(obj, "property", regObj);

            return obj.property === "RegExp";
        } finally {
            delete RegExp.prototype.value;
        }
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-86-1.js
 * @description Object.defineProperty - 'Attributes' is a Function object which implements its own [[Get]] method to access the 'configurable' property of prototype object (8.10.5 step 4.a)
 */


function testcase() {
        var obj = {};
        try {
            Function.prototype.configurable = true;
            var funObj = function (a, b) {
                return a + b;
            };

            Object.defineProperty(obj, "property", funObj);

            var beforeDeleted = obj.hasOwnProperty("property");

            delete obj.property;

            var afterDeleted = obj.hasOwnProperty("property");

            return beforeDeleted === true && afterDeleted === false;
        } finally {
            delete Function.prototype.configurable;
        }
    }
runTestCase(testcase);

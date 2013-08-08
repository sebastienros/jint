/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-120.js
 * @description Object.defineProperty - 'configurable' property in 'Attributes' is a Error object  (8.10.5 step 4.b)
 */


function testcase() {
        var obj = {};

        var attr = {
            configurable: new SyntaxError()
        };

        Object.defineProperty(obj, "property", attr);

        var beforeDeleted = obj.hasOwnProperty("property");

        delete obj.property;

        var afterDeleted = obj.hasOwnProperty("property");

        return beforeDeleted === true && afterDeleted === false;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-356.js
 * @description ES5 Attributes - property ([[Writable]] is false, [[Enumerable]] is true, [[Configurable]] is true) is deletable
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "prop", {
            value: 2010,
            writable: false,
            enumerable: true,
            configurable: true
        });
        var beforeDelete = obj.hasOwnProperty("prop");
        delete obj.prop;
        var afterDelete = obj.hasOwnProperty("prop");
        return beforeDelete && !afterDelete;
    }
runTestCase(testcase);

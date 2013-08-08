/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-94.js
 * @description Object.defineProperty will not throw TypeError when name.configurable = false, name.writable = false, desc.value and name.value are two Objects refer to the same object (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        var obj1 = { length: 10 };

        Object.defineProperty(obj, "foo", {
            value: obj1,
            writable: false,
            configurable: false 
        });

        try {
            Object.defineProperty(obj, "foo", { value: obj1 });
            return dataPropertyAttributesAreCorrect(obj, "foo", obj1, false, false, false);
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);

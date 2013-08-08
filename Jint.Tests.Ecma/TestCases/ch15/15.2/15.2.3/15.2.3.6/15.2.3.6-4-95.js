/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-95.js
 * @description Object.defineProperty will throw TypeError when name.configurable = false, name.writable = false, desc.value and name.value are two objects which refer to the different objects (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        var obj1 = { length: 10 };

        Object.defineProperty(obj, "foo", {
            value: obj1,
            writable: false,
            configurable: false 
        });

        var obj2 = { length: 20 };

        try {
            Object.defineProperty(obj, "foo", { value: obj2 });
            return false;
        } catch (e) {
            return e instanceof TypeError && dataPropertyAttributesAreCorrect(obj, "foo", obj1, false, false, false);
        }
    }
runTestCase(testcase);

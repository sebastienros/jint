/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-86.js
 * @description Object.defineProperty will throw TypeError when name.configurable = false, name.writable = false, desc.value = +0 and name.value = -0 (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: -0, 
            writable: false, 
            configurable: false 
        });

        try {
            Object.defineProperty(obj, "foo", { value: +0 });
            return false;
        } catch (e) {
            return e instanceof TypeError && dataPropertyAttributesAreCorrect(obj, "foo", -0, false, false, false);
        }
    }
runTestCase(testcase);

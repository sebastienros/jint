/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-71.js
 * @description Object.defineProperties throws TypeError when 'P' is data property and  P.configurable is false, P.writable is false, desc is data property and  desc.writable is true (8.12.9 step 10.a.i)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: 10, 
            writable: false, 
            configurable: false 
        });

        try {
            Object.defineProperties(obj, {
                foo: {
                    writable: true
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && dataPropertyAttributesAreCorrect(obj, "foo", 10, false, false, false);
        }
    }
runTestCase(testcase);

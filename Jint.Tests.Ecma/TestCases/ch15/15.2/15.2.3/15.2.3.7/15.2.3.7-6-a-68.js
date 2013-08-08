/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-68.js
 * @description Object.defineProperties throws TypeError when P is data property and  P.configurable is false, desc is accessor property (8.12.9 step 9.a)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: 10,
            configurable: false
        });

        function get_func() {
            return 11;
        }

        try {
            Object.defineProperties(obj, {
                foo: {
                    get: get_func
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && dataPropertyAttributesAreCorrect(obj, "foo", 10, false, false, false);
        }
    }
runTestCase(testcase);

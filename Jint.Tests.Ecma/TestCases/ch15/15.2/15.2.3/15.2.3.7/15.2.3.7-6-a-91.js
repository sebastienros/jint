/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-91.js
 * @description Object.defineProperties throws TypeError when P.configurable is false, both properties.[[Get]] and P.[[Get]] are two objects which refer to different objects (8.12.9 step 11.a.ii)
 */


function testcase() {

        var obj = {};

        function set_func(value) {
            obj.setVerifyHelpProp = value;
        }
        function get_func1() {
            return 10;
        }

        Object.defineProperty(obj, "foo", {
            get: get_func1,
            set: set_func,
            enumerable: false,
            configurable: false
        });

        function get_func2() {
            return 20;
        }

        try {
            Object.defineProperties(obj, {
                foo: {
                    get: get_func2
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && accessorPropertyAttributesAreCorrect(obj, "foo", get_func1, set_func, "setVerifyHelpProp", false, false);
        }

    }
runTestCase(testcase);

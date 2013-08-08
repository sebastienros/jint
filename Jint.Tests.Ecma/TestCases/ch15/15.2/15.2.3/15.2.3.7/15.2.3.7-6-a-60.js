/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-60.js
 * @description Object.defineProperties - desc.[[Set]] and P.[[Set]] are two objects which refer to the different objects (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        function set_func1() {}

        Object.defineProperty(obj, "foo", {
            set: set_func1,
            configurable: true
        });

        function set_func2(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperties(obj, {
            foo: {
                set: set_func2
            }
        });
        return accessorPropertyAttributesAreCorrect(obj, "foo", undefined, set_func2, "setVerifyHelpProp", false, true);
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-110.js
 * @description Object.defineProperties - all own properties (data property and accessor property)
 */


function testcase() {

        var obj = {};

        function get_func() {
            return 10;
        }
        function set_func(value) {
            obj.setVerifyHelpProp = value;
        }

        var properties = {
            foo1: {
                value: 200,
                enumerable: true,
                writable: true,
                configurable: true
            },
            foo2: {
                get: get_func,
                set: set_func,
                enumerable: true,
                configurable: true
            }
        };

        Object.defineProperties(obj, properties);
        return dataPropertyAttributesAreCorrect(obj, "foo1", 200, true, true, true) && accessorPropertyAttributesAreCorrect(obj, "foo2", get_func, set_func, "setVerifyHelpProp", true, true);

    }
runTestCase(testcase);

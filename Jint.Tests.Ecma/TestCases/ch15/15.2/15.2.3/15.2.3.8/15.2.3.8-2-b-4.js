/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-b-4.js
 * @description Object.seal - all own properties of 'O' are already non-configurable
 */


function testcase() {
        var obj = {};
        obj.variableForHelpVerify = "data";

        Object.defineProperty(obj, "foo1", {
            value: 10,
            writable: true,
            enumerable: true,
            configurable: false
        });

        function set_func(value) {
            obj.variableForHelpVerify = value;
        }
        function get_func() {
            return 10;
        }
        Object.defineProperty(obj, "foo2", {
            get: get_func,
            set: set_func,
            enumerable: true,
            configurable: false
        });
        var preCheck = Object.isExtensible(obj);
        Object.seal(obj);

        return preCheck && dataPropertyAttributesAreCorrect(obj, "foo1", 10, true, true, false) &&
            accessorPropertyAttributesAreCorrect(obj, "foo2", get_func, set_func, "variableForHelpVerify", true, false);
    }
runTestCase(testcase);

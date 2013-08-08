/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-c-2.js
 * @description Object.isFrozen returns false if 'O' contains own configurable accessor property
 */


function testcase() {

        var obj = {};

        function get_func() {
            return 10;
        }
        function set_func() { }

        Object.defineProperty(obj, "foo", {
            get: get_func,
            set: set_func,
            configurable: true
        });

        Object.preventExtensions(obj);
        return !Object.isFrozen(obj);

    }
runTestCase(testcase);

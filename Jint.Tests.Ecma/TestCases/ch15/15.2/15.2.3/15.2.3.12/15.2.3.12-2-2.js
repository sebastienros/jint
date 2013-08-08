/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-2.js
 * @description Object.isFrozen - inherited accessor property is not considered into the for each loop
 */


function testcase() {

        var proto = {};
        
        function get_func() {
            return 10;
        }
        function set_func() { }

        Object.defineProperty(proto, "Father", {
            get: get_func,
            set: set_func,
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        Object.preventExtensions(child);

        return Object.isFrozen(child);

    }
runTestCase(testcase);

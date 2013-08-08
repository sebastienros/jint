/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-1-s.js
 * @description StrictMode -  Writing or reading from a property named 'caller' of function objects is allowed under both strict and normal modes.
 * @onlyStrict
 */


function testcase() {
        "use strict";

        var foo = function () {
            this.caller = 12;
        }
        var obj = new foo();
        return obj.caller === 12;
    }
runTestCase(testcase);

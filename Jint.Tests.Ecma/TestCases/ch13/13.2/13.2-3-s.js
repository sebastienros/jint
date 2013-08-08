/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-3-s.js
 * @description StrictMode - Writing or reading from a property named 'arguments' of function objects is allowed under both strict and normal modes.
 * @onlyStrict
 */


function testcase() {
        "use strict";

        var foo = function () {
            this.arguments = 12;
        } 
        var obj = new foo();
        return obj.arguments === 12;
    }
runTestCase(testcase);

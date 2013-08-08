/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14.1/12.14.1-4-s.js
 * @description Strict Mode - SyntaxError isn't thrown if a TryStatement with a Catch occurs within strict code and the Identifier of the Catch production is EVAL
 * @onlyStrict
 */


function testcase() {
        "use strict";

        try {
            throw new Error("...");
            return false;
        } catch (EVAL) {
            return EVAL instanceof Error;
        }
    }
runTestCase(testcase);

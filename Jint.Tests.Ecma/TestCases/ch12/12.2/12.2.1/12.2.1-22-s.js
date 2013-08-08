/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-22-s.js
 * @description arguments as global var identifier throws SyntaxError in strict mode
 * @onlyStrict
 */




function testcase() {

    var indirectEval = eval;
	
    try {
	    indirectEval("'use strict'; var arguments;");
        return false;
	}
    catch (e) {
        return (e instanceof SyntaxError);
	}
}
runTestCase(testcase);
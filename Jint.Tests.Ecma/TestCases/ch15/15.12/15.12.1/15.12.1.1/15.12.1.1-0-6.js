/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-6.js
 * @description <BOM> is not valid JSON whitespace as specified by the production JSONWhitespace.
 */


function testcase() {
  
  try {
    JSON.parse('\ufeff1234'); // should produce a syntax error a
    }
  catch (e) {
      return true; // treat any exception as a pass, other tests ensure that JSON.parse throws SyntaxError exceptions
      }
  }
runTestCase(testcase);

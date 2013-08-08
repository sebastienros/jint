/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-8.js
 * @description U+2028 and U+2029 are not valid JSON whitespace as specified by the production JSONWhitespace.
 */


function testcase() {
  
  try {
    JSON.parse('\u2028\u20291234'); // should produce a syntax error 
    }
  catch (e) {
      return true; // treat any exception as a pass, other tests ensure that JSON.parse throws SyntaxError exceptions
      }
  }
runTestCase(testcase);

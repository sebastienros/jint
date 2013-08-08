/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-2.js
 * @description <VT> is not valid JSON whitespace as specified by the production JSONWhitespace.
 */


function testcase() {
  
  try {
    JSON.parse('\u000b1234'); // should produce a syntax error 
    }
  catch (e) {
      return true; // treat any exception as a pass, other tests ensure that JSON.parse throws SyntaxError exceptions
      }
  }
runTestCase(testcase);

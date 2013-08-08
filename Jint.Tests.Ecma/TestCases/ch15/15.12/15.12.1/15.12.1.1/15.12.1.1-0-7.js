/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-7.js
 * @description other category z spaces are not valid JSON whitespace as specified by the production JSONWhitespace.
 */


function testcase() {
  
  try {
    // the following should produce a syntax error 
    JSON.parse('\u1680\u180e\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u202f\u205f\u30001234'); 
    }
  catch (e) {
      return true; // treat any exception as a pass, other tests ensure that JSON.parse throws SyntaxError exceptions
     }
  }
runTestCase(testcase);

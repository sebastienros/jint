// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * VERTICAL TAB (U+000B) may occur within strings
 *
 * @path ch07/7.2/S7.2_A2.2_T2.js
 * @description Use real VERTICAL TAB
 */

//CHECK#1
if ("string" !== "\u000Bstr\u000Bing\u000B") {
  $ERROR('#1: "string" === "\\u000Bstr\\u000Bing\\u000B"');
}


// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If ToNumber(value) is +0, -0, +Infinity, or -Infinity,
 * return ToNumber(value)
 *
 * @path ch09/9.4/S9.4_A2.js
 * @description Check what position is defined by Number.NaN in string "abc": "abc".charAt(Number.NaN)
 */

// CHECK#1
if ("abc".charAt(0.0) !== "a") {
  $ERROR('#1: "abc".charAt(0.0) === "a". Actual: ' + ("abc".charAt(0.0)));
}

// CHECK#2
if ("abc".charAt(-0.0) !== "a") {
  $ERROR('#2: "abc".charAt(-0.0) === "a". Actual: ' + ("abc".charAt(-0.0)));
}

